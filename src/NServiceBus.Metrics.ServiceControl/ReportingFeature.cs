using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Features;
using NServiceBus.Hosting;
using NServiceBus.Metrics.ServiceControl.ServiceControlReporting;
using NServiceBus.ObjectBuilder;
using NServiceBus.Transport;
using ServiceControl.Monitoring.Data;

namespace NServiceBus.Metrics.ServiceControl
{
    using MessageMutator;

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

            var reportingOptions = ReportingOptions.Get(options);

            SetUpServiceControlReporting(context, reportingOptions, endpointName, metrics);
        }

        void SetUpServiceControlReporting(FeatureConfigurationContext context, ReportingOptions options, string endpointName, Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> durations)
        {
            var metricsContext = new MetricsContext(endpointName);
            SetUpQueueLengthReporting(context, metricsContext);

            var baseHeadersFactory = new MetricsReportBaseHeaderFactory(endpointName, options);

            context.RegisterStartupTask(builder =>
            {
                var hostInformation = builder.Build<HostInformation>();

                var headers = baseHeadersFactory.BuildBaseHeaders(hostInformation);

                return new ServiceControlReporting(metricsContext, builder, options, headers);
            });

            context.RegisterStartupTask(builder =>
            {
                var hostInformation = builder.Build<HostInformation>();

                var headers = baseHeadersFactory.BuildBaseHeaders(hostInformation);

                var rawDataReporterFactory = new RawDataReporterFactory(builder, options, headers);

                return new ServiceControlRawDataReporting(rawDataReporterFactory, durations);
            });

            SetUpOutgoingMessageMutator(context, options);
        }

        static void SetUpQueueLengthReporting(FeatureConfigurationContext context, MetricsContext metricsContext)
        {
            QueueLengthTracker.SetUp(metricsContext, context);
        }

        void SetUpOutgoingMessageMutator(FeatureConfigurationContext context, ReportingOptions options)
        {
            if (options.TryGetValidEndpointInstanceIdOverride(out var instanceId))
            {
                context.Container.ConfigureComponent(() => new MetricsIdAttachingMutator(instanceId), DependencyLifecycle.SingleInstance);
            }
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
            public ServiceControlRawDataReporting(RawDataReporterFactory rawDataReporterFactory, Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> metrics)
            {
                this.rawDataReporterFactory = rawDataReporterFactory;
                this.metrics = metrics;

                reporters = new List<RawDataReporter>();
            }

            protected override Task OnStart(IMessageSession session)
            {
                foreach (var metric in metrics)
                {
                    reporters.Add(rawDataReporterFactory.CreateReporter(metric.Key, metric.Value.Item1, metric.Value.Item2));
                }

                foreach (var reporter in reporters)
                {
                    reporter.Start();
                }

                return Task.FromResult(0);
            }

            protected override async Task OnStop(IMessageSession session)
            {
                await Task.WhenAll(reporters.Select(r => r.Stop())).ConfigureAwait(false);

                foreach (var reporter in reporters)
                {
                    reporter.Dispose();
                }
            }

            readonly RawDataReporterFactory rawDataReporterFactory;
            readonly Dictionary<string, Tuple<RingBuffer, TaggedLongValueWriterV1>> metrics;
            readonly List<RawDataReporter> reporters;
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