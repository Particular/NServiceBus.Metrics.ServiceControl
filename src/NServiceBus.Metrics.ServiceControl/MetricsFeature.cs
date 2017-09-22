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

            var reportingOptions = ReportingOptions.Get(options);
            
            SetUpServiceControlReporting(context, reportingOptions, endpointName);
        }

        static void SetUpServiceControlReporting(FeatureConfigurationContext context, ReportingOptions options, string endpointName)
        {
            if (!string.IsNullOrEmpty(options.ServiceControlMetricsAddress))
            {
                var metricsContext = new Context();
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

                    return new ServiceControlRawDataReporting(builder, options, headers);
                });
            }
        }

        static void SetUpQueueLengthReporting(FeatureConfigurationContext context, Context metricsContext)
        {
            QueueLengthTracker.SetUp(metricsContext, context);
        }

        class ServiceControlReporting : FeatureStartupTask
        {
            public ServiceControlReporting(Context metricsContext, IBuilder builder, ReportingOptions options, Dictionary<string, string> headers)
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
            readonly Context metricsContext;
            readonly IBuilder builder;
            readonly ReportingOptions options;
            readonly Dictionary<string, string> headers;
            Task task;
        }

        class ServiceControlRawDataReporting : FeatureStartupTask
        {
            const string TaggedValueMetricContentType = "TaggedLongValueWriterOccurrence";

            public ServiceControlRawDataReporting(IBuilder builder, ReportingOptions options, Dictionary<string, string> headers)
            {
                this.builder = builder;
                this.options = options;
                this.headers = headers;

                reporters = new List<RawDataReporter>();
            }

            protected override Task OnStart(IMessageSession session)
            {
                reporters.Add(CreateReporter(out options.CriticalTimeHandler, "CriticalTime"));
                reporters.Add(CreateReporter(out options.ProcessingTimeHandler, "ProcessingTime"));

                reporters.Add(CreateReporter(out options.RetryHandler, "Retries"));

                foreach (var reporter in reporters)
                {
                    reporter.Start();
                }

                return Task.FromResult(0);
            }

            RawDataReporter CreateReporter(out OnEvent<DurationEvent> handler, string metricType)
            {
                var writer = new TaggedLongValueWriterV1();

                var reporter = CreateReporter(metricType,
                    TaggedValueMetricContentType,
                    (entries, binaryWriter) => writer.Write(binaryWriter, entries), out var buffer);

                handler = (ref DurationEvent e) =>
                {
                    var tag = writer.GetTagId(e.MessageType ?? "");
                    RingBufferExtensions.WriteTaggedValue(buffer, metricType, (long)e.Duration.TotalMilliseconds, tag);
                };

                return reporter;
            }

            RawDataReporter CreateReporter(out OnEvent<SignalEvent> handler, string metricType)
            {
                var writer = new TaggedLongValueWriterV1();

                var reporter = CreateReporter(
                    metricType,
                    TaggedValueMetricContentType,
                    (entries, binaryWriter) => writer.Write(binaryWriter, entries), out var buffer);

                handler = (ref SignalEvent e) =>
                {
                    var tag = writer.GetTagId(e.MessageType ?? "");
                    RingBufferExtensions.WriteTaggedValue(buffer, metricType, 1, tag);
                };

                return reporter;
            }

            RawDataReporter CreateReporter(string metricType, string contentType, WriteOutput outputWriter, out RingBuffer buffer)
            {
                buffer = new RingBuffer();

                var reporterHeaders = new Dictionary<string, string>(headers)
                {
                    {Headers.ContentType, contentType},
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

                return new RawDataReporter(Sender, buffer, outputWriter);
            }

            protected override async Task OnStop(IMessageSession session)
            {
                await Task.WhenAll(reporters.Select(r => r.Stop())).ConfigureAwait(false);

                foreach (var reporter in reporters)
                {
                    reporter.Dispose();
                }
            }

            readonly ProbeContext probeContext;
            readonly IBuilder builder;
            readonly ReportingOptions options;
            readonly Dictionary<string, string> headers;
            readonly List<RawDataReporter> reporters;

            static readonly ILog log = LogManager.GetLogger<ServiceControlRawDataReporting>();
        }
    }
}