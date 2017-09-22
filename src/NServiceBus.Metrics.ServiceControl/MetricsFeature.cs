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
        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings;
            var options = settings.Get<MetricsOptions>();
            var endpointName = settings.EndpointName();
            var reportingOptions = ReportingOptions.Get(options);
            
            SetUpServiceControlReporting(context, reportingOptions, endpointName, probeContext);
        }

        static void SetUpServiceControlReporting(FeatureConfigurationContext context, ReportingOptions options, string endpointName, ProbeContext probeContext)
        {
            if (!string.IsNullOrEmpty(options.ServiceControlMetricsAddress))
            {
                var metricsContext = new Context();

                SetUpQueueLengthReporting(context, metricsContext);

                Func<IBuilder, Dictionary<string, string>> buildBaseHeaders = b =>
                {
                    var hostInformation = b.Build<HostInformation>();

                    var headers = new Dictionary<string, string>
                    {
                        {Headers.OriginatingEndpoint, endpointName},
                        {Headers.OriginatingMachine, RuntimeEnvironment.MachineName},
                        {Headers.OriginatingHostId, hostInformation.HostId.ToString("N")},
                        {Headers.HostDisplayName, hostInformation.DisplayName },
                    };

                    if (options.TryGetValidEndpointInstanceIdOverride(out var instanceId))
                    {
                        headers.Add(MetricHeaders.MetricInstanceId, instanceId);
                    }

                    return headers;
                };

                context.RegisterStartupTask(builder =>
                {
                    var headers = buildBaseHeaders(builder);

                    return new ServiceControlReporting(metricsContext, builder, options, headers);
                });

                context.RegisterStartupTask(builder =>
                {
                    var headers = buildBaseHeaders(builder);

                    return new ServiceControlRawDataReporting(probeContext, builder, options, headers);
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

            public ServiceControlRawDataReporting(ProbeContext probeContext, IBuilder builder, ReportingOptions options, Dictionary<string, string> headers)
            {
                this.probeContext = probeContext;
                this.builder = builder;
                this.options = options;
                this.headers = headers;

                reporters = new List<RawDataReporter>();
            }

            protected override Task OnStart(IMessageSession session)
            {
                foreach (var durationProbe in probeContext.Durations)
                {
                    if (durationProbe.Name == ProcessingTimeProbeBuilder.ProcessingTime ||
                        durationProbe.Name == CriticalTimeProbeBuilder.CriticalTime)
                    {
                        reporters.Add(CreateReporter(durationProbe));
                    }
                }

                foreach (var signalProbe in probeContext.Signals)
                {
                    if (signalProbe.Name == RetriesProbeBuilder.Retries)
                    {
                        reporters.Add(CreateReporter(signalProbe));
                    }
                }

                foreach (var reporter in reporters)
                {
                    reporter.Start();
                }

                return Task.FromResult(0);
            }

            RawDataReporter CreateReporter(IDurationProbe probe)
            {
                var metricType = GetMetricType(probe);
                var writer = new TaggedLongValueWriterV1();

                return CreateReporter(
                    writeAction => probe.Register((ref DurationEvent d) =>
                    {
                        var tag = writer.GetTagId(d.MessageType ?? "");
                        writeAction((long)d.Duration.TotalMilliseconds, tag);
                    }),
                    metricType,
                    TaggedValueMetricContentType,
                    (entries, binaryWriter) => writer.Write(binaryWriter, entries));
            }

            RawDataReporter CreateReporter(ISignalProbe probe)
            {
                var metricType = GetMetricType(probe);
                var writer = new TaggedLongValueWriterV1();

                return CreateReporter(
                    writeAction => probe.Register((ref SignalEvent e) =>
                    {
                        var tag = writer.GetTagId(e.MessageType ?? "");
                        writeAction(1, tag);
                    }),
                    metricType,
                    TaggedValueMetricContentType,
                    (entries, binaryWriter) => writer.Write(binaryWriter, entries));
            }

            static string GetMetricType(IProbe probe) => $"{probe.Name.Replace(" ", string.Empty)}";

            RawDataReporter CreateReporter(Action<Action<long, int>> setupProbe, string metricType, string contentType, WriteOutput outputWriter)
            {
                var buffer = new RingBuffer();

                var reporterHeaders = new Dictionary<string, string>(headers)
                {
                    {Headers.ContentType, contentType},
                    {MetricHeaders.MetricType, metricType}
                };
            
                var dispatcher = builder.Build<IDispatchMessages>();
                var address = new UnicastAddressTag(options.ServiceControlMetricsAddress);

                Func<byte[], Task> sender = async body =>
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
                };

                var reporter = new RawDataReporter(sender, buffer, outputWriter);

                setupProbe((value, tag) =>
                {
                    var written = false;
                    var attempts = 0;

                    while (!written)
                    {
                        written = buffer.TryWrite(value, tag);

                        attempts++;

                        if (attempts >= MaxExpectedWriteAttempts)
                        {
                            log.Warn($"Failed to buffer metrics data for ${metricType} after ${attempts} attempts.");
                            attempts = 0;
                        }
                    }
                });

                return reporter;
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

            const int MaxExpectedWriteAttempts = 10;

            static readonly ILog log = LogManager.GetLogger<ServiceControlRawDataReporting>();
        }
    }
}