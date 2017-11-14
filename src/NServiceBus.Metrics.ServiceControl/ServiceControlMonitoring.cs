namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Features;
    using global::ServiceControl.Monitoring.Data;
    using Logging;
    using Pipeline;
    using Pipeline.Contexts;
    using Support;
    using Transports;
    using Unicast;

    class ServiceControlMonitoring : Feature
    {
        static readonly ILog log = LogManager.GetLogger<ServiceControlMonitoring>();

        public ServiceControlMonitoring()
        {
            EnableByDefault();
            Prerequisite(c => c.Settings.HasSetting<ReportingOptions>(), "Metrics should be configured");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var buffers = new Buffers();
            var options = context.Settings.Get<ReportingOptions>();
            var container = context.Container;

            container.ConfigureComponent(() => options, DependencyLifecycle.SingleInstance);
            container.ConfigureComponent(() => buffers, DependencyLifecycle.SingleInstance);
            context.Pipeline.Register<ServiceControlMonitoringRegistration>();
            RegisterStartupTask<ReportingStartupTask>();
        }

        class ServiceControlMonitoringRegistrationBehavior : IBehavior<IncomingContext>
        {
            readonly Buffers buffers;

            public ServiceControlMonitoringRegistrationBehavior(Buffers buffers)
            {
                this.buffers = buffers;
            }

            public void Invoke(IncomingContext context, Action next)
            {
                next();

                var started = context.Get<DateTime>("IncomingMessage.ProcessingStarted");
                var ended = context.Get<DateTime>("IncomingMessage.ProcessingEnded");

                var processingTime = ended - started;

                if (context.PhysicalMessage.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageType))
                {
                    buffers.ReportProcessingTime(processingTime, messageType);

                    if (context.TryGet("IncomingMessage.TimeSent", out DateTime timeSent))
                    {
                        var criticalTime = ended - timeSent;
                        buffers.ReportCriticalTime(criticalTime, messageType);
                    }
                }
            }
        }

        class ServiceControlMonitoringRegistration : RegisterStep
        {
            public ServiceControlMonitoringRegistration()
                : base("InvokeMetrics", typeof(ServiceControlMonitoringRegistrationBehavior), "Invokes ServiceControlMonitoring logic")
            {
                InsertBefore(WellKnownStep.ProcessingStatistics);
            }
        }

        class ReportingStartupTask : FeatureStartupTask
        {
            const string TaggedValueMetricContentType = "TaggedLongValueWriterOccurrence";

            readonly Buffers buffers;
            readonly ISendMessages dispatcher;
            readonly Dictionary<string, string> headers;
            readonly ReportingOptions options;
            RawDataReporter[] reporters;

            public ReportingStartupTask(Buffers buffers, UnicastBus bus, ISendMessages dispatcher, Configure config, ReportingOptions options)
            {
                this.buffers = buffers;
                this.dispatcher = dispatcher;

#pragma warning disable 618
                var hostInformation = bus.HostInformation;
#pragma warning restore 618

                headers = new Dictionary<string, string>
                {
                    {Headers.OriginatingEndpoint, config.Settings.EndpointName()},
                    {Headers.OriginatingMachine, RuntimeEnvironment.MachineName},
                    {Headers.OriginatingHostId, hostInformation.HostId.ToString("N")},
                    {Headers.HostDisplayName, hostInformation.DisplayName },
                };

                this.options = options;
                if (this.options.TryGetValidEndpointInstanceIdOverride(out var instanceId))
                {
                    headers.Add(MetricHeaders.MetricInstanceId, instanceId);
                }
            }

            protected override void OnStart()
            {
                reporters = new[]
                {
                    BuildReporter("ProcessingTime", buffers.ProcessingTime),
                };
            }

            RawDataReporter BuildReporter(string metricType, Buffer buffer)
            {
                var reporter = new RawDataReporter(BuildSend(CreateHeaders(metricType)), buffer.Ring, (entries, binaryWriter) => buffer.Writer.Write(binaryWriter, entries));
                reporter.Start();
                return reporter;
            }

            protected override void OnStop()
            {
                Task.WhenAll(reporters.Select(r => r.Stop())).GetAwaiter().GetResult();
            }

            Func<byte[], Task> BuildSend(Dictionary<string, string> headers)
            {
                var completed = Task.FromResult(0);
                return body =>
                {
                    var operation = new TransportMessage(Guid.NewGuid().ToString(), headers)
                    {
                        Body = body,
                        MessageIntent = MessageIntentEnum.Send,
                    };
                    try
                    {
                        dispatcher.Send(operation, new SendOptions(options.ServiceControlMetricsAddress));
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error while reporting raw data to {options.ServiceControlMetricsAddress}.", ex);
                    }

                    return completed;
                };
            }

            Dictionary<string, string> CreateHeaders(string metricType)
            {
                return new Dictionary<string, string>(headers)
                {
                    {Headers.ContentType, TaggedValueMetricContentType},
                    {MetricHeaders.MetricType, metricType}
                };
            }
        }
    }
}