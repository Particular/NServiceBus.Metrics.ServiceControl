using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Features;
using NServiceBus.Hosting;
using NServiceBus.Logging;
using NServiceBus.Metrics.ServiceControl.ServiceControlReporting;
using NServiceBus.ObjectBuilder;
using NServiceBus.Routing;
using NServiceBus.Support;
using NServiceBus.Transport;
using ServiceControl.Monitoring.Data;

namespace NServiceBus.Metrics.ServiceControl
{
    using DeliveryConstraints;
    using MessageMutator;
    using Performance.TimeToBeReceived;

    class ReportingFeature : Feature
    {
        public ReportingFeature()
        {
            EnableByDefault();
            Prerequisite(ctx =>
            {
                var options = ctx.Settings.GetOrDefault<MetricsOptions>();
                if (options == null)
                {
                    return false;
                }

                var reportingOptions = ReportingOptions.Get(options);
                var address = reportingOptions.ServiceControlMetricsAddress;
                return string.IsNullOrEmpty(address) == false;
            }, $"Reporting is enabled by calling '{nameof(MetricsOptionsExtensions.SendMetricDataToServiceControl)}'");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings;
            var options = settings.Get<MetricsOptions>();
            var endpointName = settings.EndpointName();

            var metrics = new Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>>();
            void RegisterDuration(IDurationProbe probe)
            {
                var buffer = new RingBuffer();
                var writer = new TaggedLongValueWriterV1();
                var name = probe.Name.Replace(" ", "");

                probe.Register((ref DurationEvent e) =>
                {
                    var tag = writer.GetTagId(e.MessageType ?? "");
                    RingBufferExtensions.WriteTaggedValue(buffer, name, (long)e.Duration.TotalMilliseconds, tag);
                });
                metrics[name] = Tuple.Create(buffer, writer);
            }

            void RegisterSignal(ISignalProbe probe)
            {
                var buffer = new RingBuffer();
                var writer = new TaggedLongValueWriterV1();
                var name = probe.Name.Replace(" ", "");

                probe.Register((ref SignalEvent e) =>
                {
                    var tag = writer.GetTagId(e.MessageType ?? "");
                    RingBufferExtensions.WriteTaggedValue(buffer, name, 1, tag);
                });
                metrics[name] = Tuple.Create(buffer, writer);
            }

            options.RegisterObservers(probeContext =>
            {
                foreach (var durationProbe in probeContext.Durations)
                {
                    var name = durationProbe.Name;
                    if (name == "Processing Time" || name == "Critical Time")
                    {
                        RegisterDuration(durationProbe);
                    }
                }

                foreach (var signalProbe in probeContext.Signals)
                {
                    if (signalProbe.Name == "Retries")
                    {
                        RegisterSignal(signalProbe);
                    }
                }
            });

            var queueLengthMetric = SetupQueueLengthReporting(context);

            metrics.Add("QueueLength", queueLengthMetric);

            var reportingOptions = ReportingOptions.Get(options);

            SetUpServiceControlReporting(context, reportingOptions, endpointName, metrics);
        }

        static Tuple<RingBuffer, TaggedLongValueWriterV1> SetupQueueLengthReporting(FeatureConfigurationContext context)
        {
            var queueLengthBuffer = new RingBuffer();
            var queueLengthWriter = new TaggedLongValueWriterV1();
            var localAddress = context.Settings.LocalAddress();
            var queueLengthReporter = new QueueLengthBufferReporter(queueLengthBuffer, queueLengthWriter, localAddress);

            context.Container.RegisterSingleton<IReportNativeQueueLength>(queueLengthReporter);

            return Tuple.Create(queueLengthBuffer, queueLengthWriter);
        }

        void SetUpServiceControlReporting(FeatureConfigurationContext context, ReportingOptions options, string endpointName, Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> durations)
        {
            var endpointMetadata = new EndpointMetadata(context.Settings.LocalAddress());

            Dictionary<string, string> BuildBaseHeaders(IBuilder b)
            {
                var hostInformation = b.Build<HostInformation>();

                var headers = new Dictionary<string, string>
                {
                    {Headers.OriginatingEndpoint, endpointName},
                    {Headers.OriginatingMachine, RuntimeEnvironment.MachineName},
                    {Headers.OriginatingHostId, hostInformation.HostId.ToString("N")},
                    {Headers.HostDisplayName, hostInformation.DisplayName},
                };

                if (options.TryGetValidEndpointInstanceIdOverride(out var instanceId))
                {
                    headers.Add(MetricHeaders.MetricInstanceId, instanceId);
                }

                return headers;
            }

            context.RegisterStartupTask(builder =>
            {
                var headers = BuildBaseHeaders(builder);

                return new ServiceControlMetadataReporting(endpointMetadata, builder, options, headers);
            });

            context.RegisterStartupTask(builder =>
            {
                var headers = BuildBaseHeaders(builder);

                return new ServiceControlRawDataReporting(builder, options, headers, durations);
            });

            SetUpOutgoingMessageMutator(context, options);
        }

        void SetUpOutgoingMessageMutator(FeatureConfigurationContext context, ReportingOptions options)
        {
            if (options.TryGetValidEndpointInstanceIdOverride(out var instanceId))
            {
                context.Container.ConfigureComponent(() => new MetricsIdAttachingMutator(instanceId), DependencyLifecycle.SingleInstance);
            }
        }

        class ServiceControlMetadataReporting : FeatureStartupTask
        {
            public ServiceControlMetadataReporting(EndpointMetadata endpointMetadata, IBuilder builder, ReportingOptions options, Dictionary<string, string> headers)
            {
                this.endpointMetadata = endpointMetadata;
                this.builder = builder;
                this.options = options;
                this.headers = headers;

                headers.Add(Headers.EnclosedMessageTypes, "NServiceBus.Metrics.EndpointMetadataReport");
                headers.Add(Headers.ContentType, ContentTypes.Json);
            }

            protected override Task OnStart(IMessageSession session)
            {
                var serviceControlReport = new NServiceBusMetadataReport(builder.Build<IDispatchMessages>(), options, headers, endpointMetadata);

                task = Task.Run(async () =>
                {
                    while (cancellationTokenSource.IsCancellationRequested == false)
                    {
                        await serviceControlReport.RunReportAsync().ConfigureAwait(false);

                        try
                        {
                            await Task.Delay(options.ServiceControlReportingInterval, cancellationTokenSource.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // shutdown
                            return;
                        }
                    }
                });

                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                cancellationTokenSource.Cancel();
                return task;
            }

            readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            readonly EndpointMetadata endpointMetadata;
            readonly IBuilder builder;
            readonly ReportingOptions options;
            readonly Dictionary<string, string> headers;
            Task task;
        }

        class ServiceControlRawDataReporting : FeatureStartupTask
        {
            const string TaggedValueMetricContentType = "TaggedLongValueWriterOccurrence";

            public ServiceControlRawDataReporting(IBuilder builder, ReportingOptions options, Dictionary<string, string> headers, Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> metrics)
            {
                this.builder = builder;
                this.options = options;
                this.headers = headers;
                this.metrics = metrics;

                reporters = new List<RawDataReporter>();
            }

            protected override Task OnStart(IMessageSession session)
            {
                options.OnCreateReporters(() =>
                {
                    foreach (var metric in metrics)
                    {
                        reporters.Add(CreateReporter(metric.Key, metric.Value.Item1, metric.Value.Item2));
                    }

                    foreach (var reporter in reporters)
                    {
                        reporter.Start();
                    }
                });                

                return Task.FromResult(0);
            }

            RawDataReporter CreateReporter(string metricType, RingBuffer buffer, TaggedLongValueWriterV1 writer)
            {
                var reporterHeaders = new Dictionary<string, string>(headers)
                {
                    {Headers.ContentType, TaggedValueMetricContentType},
                    {MetricHeaders.MetricType, metricType}
                };

                var dispatcher = builder.Build<IDispatchMessages>();
                var address = new UnicastAddressTag(options.ServiceControlMetricsAddress);

                async Task Sender(byte[] body)
                {
                    var message = new OutgoingMessage(Guid.NewGuid().ToString(), reporterHeaders, body);
                    var constraints = new List<DeliveryConstraint>
                    {
                        new DiscardIfNotReceivedBefore(options.TimeToBeReceived)
                    };
                    var operation = new TransportOperation(message, address, DispatchConsistency.Default, constraints);
                    try
                    {
                        await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag())
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error while reporting raw data to {options.ServiceControlMetricsAddress}.", ex);
                    }
                }

                return new RawDataReporter(Sender, buffer, (entries, binaryWriter) => writer.Write(binaryWriter, entries), 2048, options.ServiceControlReportingInterval);
            }

            protected override async Task OnStop(IMessageSession session)
            {
                await Task.WhenAll(reporters.Select(r => r.Stop())).ConfigureAwait(false);

                foreach (var reporter in reporters)
                {
                    reporter.Dispose();
                }
            }

            readonly IBuilder builder;
            readonly ReportingOptions options;
            readonly Dictionary<string, string> headers;
            readonly Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> metrics;
            readonly List<RawDataReporter> reporters;

            static readonly ILog log = LogManager.GetLogger<ServiceControlRawDataReporting>();
        }

        class MetricsIdAttachingMutator : IMutateOutgoingMessages
        {
            readonly string instanceId;

            public MetricsIdAttachingMutator(string instanceId)
            {
                this.instanceId = instanceId;
            }

            public Task MutateOutgoing(MutateOutgoingMessageContext context)
            {
                context.OutgoingHeaders[MetricHeaders.MetricInstanceId] = instanceId;
                return TaskExtensions.Completed;
            }
        }
    }
}