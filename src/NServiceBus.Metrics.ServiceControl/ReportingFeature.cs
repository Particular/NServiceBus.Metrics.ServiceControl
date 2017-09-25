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
    class ReportingFeature : Feature
    {
        public ReportingFeature()
        {
            EnableByDefault();
            Prerequisite(ctx=>
            {
                var options = ctx.Settings.GetOrDefault<MetricsOptions>();
                if (options == null)
                {
                    return false;
                }

                var reportingOptions = ReportingOptions.Get(options);
                var address = reportingOptions.ServiceControlMetricsAddress;
                return string.IsNullOrEmpty(address) == false;
            }, $"Reporting is enabled by calling '{nameof(MetricsOptionsExtensions.SendMetricDataToServiceControl2)}'");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings;
            var options = settings.Get<MetricsOptions>();
            var endpointName = settings.EndpointName();

            var metrics = new Dictionary<string, Tuple<RingBuffer,TaggedLongValueWriterV1>>();
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

            var reportingOptions = ReportingOptions.Get(options);
            
            SetUpServiceControlReporting(context, reportingOptions, endpointName, metrics);
        }

        static void SetUpServiceControlReporting(FeatureConfigurationContext context, ReportingOptions options, string endpointName, Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> durations)
        {
            if (!string.IsNullOrEmpty(options.ServiceControlMetricsAddress))
            {
                var metricsContext = new MetricsContext(endpointName);
                SetUpQueueLengthReporting(context, metricsContext);

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

                    return new ServiceControlReporting(metricsContext, builder, options, headers);
                });

                context.RegisterStartupTask(builder =>
                {
                    var headers = BuildBaseHeaders(builder);

                    return new ServiceControlRawDataReporting(builder, options, headers, durations);
                });
            }
        }

        static void SetUpQueueLengthReporting(FeatureConfigurationContext context, MetricsContext metricsContext)
        {
            QueueLengthTracker.SetUp(metricsContext, context);
        }

        class ServiceControlReporting : FeatureStartupTask
        {
            public ServiceControlReporting(MetricsContext metricsContext, IBuilder builder, ReportingOptions options, Dictionary<string, string> headers)
            {
                this.metricsContext = metricsContext;
                this.builder = builder;
                this.options = options;
                this.headers = headers;

                headers.Add(Headers.EnclosedMessageTypes, "NServiceBus.Metrics.MetricReport");
                headers.Add(Headers.ContentType, ContentTypes.Json);
            }

            protected override Task OnStart(IMessageSession session)
            {
                var serviceControlReport = new NServiceBusMetricReport(builder.Build<IDispatchMessages>(), options, headers, metricsContext);

                task = Task.Run(async () =>
                {
                    while (cancellationTokenSource.IsCancellationRequested == false)
                    {
                        await serviceControlReport.RunReportAsync().ConfigureAwait(false);
                        await Task.Delay(options.ServiceControlReportingInterval).ConfigureAwait(false);
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
            readonly MetricsContext metricsContext;
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
                foreach (var metric in metrics)
                {
                    reporters.Add(CreateReporter( metric.Key, metric.Value.Item1, metric.Value.Item2));
                }

                foreach (var reporter in reporters)
                {
                    reporter.Start();
                }

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

                    var operation = new TransportOperation(message, address);
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

                return new RawDataReporter(Sender, buffer, (entries, binaryWriter) => writer.Write(binaryWriter, entries));
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
    }
}