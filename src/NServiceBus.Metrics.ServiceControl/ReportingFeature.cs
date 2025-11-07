namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Hosting;
    using Logging;
    using MessageMutator;
    using Microsoft.Extensions.DependencyInjection;
    using Performance.TimeToBeReceived;
    using Routing;
    using ServiceControlReporting;
    using Support;
    using Transport;

    sealed class ReportingFeature : Feature
    {
        public ReportingFeature()
        {
            DependsOn<MetricsFeature>();
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

            Defaults(settings =>
            {
                var options = settings.GetOrDefault<MetricsOptions>();
                if (options != null)
                {
                    var reportingOptions = ReportingOptions.Get(options);
                    settings.Set("NServiceBus.Metrics.ServiceControl.MetricsAddress", reportingOptions.ServiceControlMetricsAddress);
                    settings.AddStartupDiagnosticsSection("Manifest-MonitoringQueue", reportingOptions.ServiceControlMetricsAddress);
                }
            });
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
                    if (name is "Processing Time" or "Critical Time")
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

            context.Services.AddSingleton<IReportNativeQueueLength>(sp =>
                new QueueLengthBufferReporter(
                    queueLengthBuffer,
                    queueLengthWriter,
                    sp.GetRequiredService<ReceiveAddresses>().MainReceiveAddress));

            return Tuple.Create(queueLengthBuffer, queueLengthWriter);
        }

        void SetUpServiceControlReporting(FeatureConfigurationContext context, ReportingOptions options, string endpointName, Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> durations)
        {
            Dictionary<string, string> BuildBaseHeaders(IServiceProvider b)
            {
                var hostInformation = b.GetRequiredService<HostInformation>();

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
                var receiveAddresses = builder.GetRequiredService<ReceiveAddresses>();

                return new ServiceControlMetadataReporting(receiveAddresses.MainReceiveAddress, builder, options, headers);
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
                context.Services.AddSingleton<IMutateOutgoingMessages>(new MetricsIdAttachingMutator(instanceId));
            }
        }

        class ServiceControlMetadataReporting : FeatureStartupTask
        {
            public ServiceControlMetadataReporting(string receiveAddress, IServiceProvider builder, ReportingOptions options, Dictionary<string, string> headers)
            {
                endpointMetadata = new EndpointMetadata(receiveAddress);
                this.builder = builder;
                this.options = options;
                this.headers = headers;

                headers.Add(Headers.EnclosedMessageTypes, "NServiceBus.Metrics.EndpointMetadataReport");
                headers.Add(Headers.ContentType, ContentTypes.Json);
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                var serviceControlReport = new NServiceBusMetadataReport(builder.GetRequiredService<IMessageDispatcher>(), options, headers, endpointMetadata);

                // CancellationToken.None because otherwise the task simply won't start if the token is canceled
                task = Task.Run(
                    async () =>
                    {
                        while (!cancellationTokenSource.IsCancellationRequested)
                        {
                            try
                            {
                                await serviceControlReport.RunReportAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                                await Task.Delay(options.ServiceControlReportingInterval, cancellationTokenSource.Token).ConfigureAwait(false);
                            }
                            catch (Exception ex) when (ex.IsCausedBy(cancellationTokenSource.Token))
                            {
                                // private token, reporting is being stopped, log the exception in case the stack trace is ever needed for debugging
                                Log.Debug("Operation canceled while stopping ServiceControl metadata reporting.", ex);
                                break;
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Failed to report metrics to ServiceControl.", ex);
                            }
                        }
                    },
                    CancellationToken.None);

                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
            {
                cancellationTokenSource.Cancel();
                return task;
            }

            readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            readonly EndpointMetadata endpointMetadata;
            readonly IServiceProvider builder;
            readonly ReportingOptions options;
            readonly Dictionary<string, string> headers;
            Task task;

            static readonly ILog Log = LogManager.GetLogger<ServiceControlMetadataReporting>();
        }

        class ServiceControlRawDataReporting : FeatureStartupTask
        {
            const string TaggedValueMetricContentType = "TaggedLongValueWriterOccurrence";

            public ServiceControlRawDataReporting(IServiceProvider builder, ReportingOptions options, Dictionary<string, string> headers, Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> metrics)
            {
                this.builder = builder;
                this.options = options;
                this.headers = headers;
                this.metrics = metrics;

                reporters = [];
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                foreach (var metric in metrics)
                {
                    reporters.Add(CreateReporter(metric.Key, metric.Value.Item1, metric.Value.Item2));
                }

                foreach (var reporter in reporters)
                {
                    reporter.Start();
                }

                return Task.CompletedTask;
            }

            RawDataReporter CreateReporter(string metricType, RingBuffer buffer, TaggedLongValueWriterV1 writer)
            {
                var reporterHeaders = new Dictionary<string, string>(headers)
                {
                    {Headers.ContentType, TaggedValueMetricContentType},
                    {MetricHeaders.MetricType, metricType}
                };

                var dispatcher = builder.GetRequiredService<IMessageDispatcher>();
                var address = new UnicastAddressTag(options.ServiceControlMetricsAddress);

                async Task Sender(byte[] body, CancellationToken cancellationToken)
                {
                    var message = new OutgoingMessage(Guid.NewGuid().ToString(), reporterHeaders, body);

                    var dispatchProperties = new DispatchProperties
                    {
                        DiscardIfNotReceivedBefore = new DiscardIfNotReceivedBefore(options.TimeToBeReceived),
                    };

                    var operation = new TransportOperation(message, address, dispatchProperties, DispatchConsistency.Default);

                    try
                    {
                        await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), cancellationToken)
                            .ConfigureAwait(false);

                        if (Log.IsDebugEnabled)
                        {
                            Log.Debug($"Sent {body.Length} bytes for {metricType} metric.");
                        }
                    }
                    catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
                    {
                        Log.Error($"Error while reporting raw data to {options.ServiceControlMetricsAddress}.", ex);
                    }
                }

                return new RawDataReporter(Sender, buffer, (entries, binaryWriter) => writer.Write(binaryWriter, entries), 2048, options.ServiceControlReportingInterval);
            }

            protected override async Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(reporters.Select(r => r.Stop())).ConfigureAwait(false);

                foreach (var reporter in reporters)
                {
                    reporter.Dispose();
                }
            }

            readonly IServiceProvider builder;
            readonly ReportingOptions options;
            readonly Dictionary<string, string> headers;
            readonly Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> metrics;
            readonly List<RawDataReporter> reporters;

            static readonly ILog Log = LogManager.GetLogger<ServiceControlRawDataReporting>();
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
                return Task.CompletedTask;
            }
        }
    }
}
