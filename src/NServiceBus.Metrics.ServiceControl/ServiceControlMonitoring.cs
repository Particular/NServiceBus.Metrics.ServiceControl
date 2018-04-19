namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Faults;
    using Features;
    using global::ServiceControl.Monitoring.Data;
    using Hosting;
    using Logging;
    using Pipeline;
    using Pipeline.Contexts;
    using ServiceControlReporting;
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
            var settings = context.Settings;

            var options = settings.Get<ReportingOptions>();
            var container = context.Container;

            container.ConfigureComponent(() => new QueueLengthBufferReporter(buffers), DependencyLifecycle.SingleInstance);
            container.ConfigureComponent(() => options, DependencyLifecycle.SingleInstance);
            container.ConfigureComponent(builder =>
            {
                var notifications = builder.Build<BusNotifications>();
                var observer = new ErrorObserver(buffers.ReportRetry);

                notifications.Errors
                    .MessageHasBeenSentToSecondLevelRetries
                    .Subscribe(observer);

                notifications.Errors
                    .MessageHasFailedAFirstLevelRetryAttempt
                    .Subscribe(observer);

                return buffers;

            }, DependencyLifecycle.SingleInstance);

            var endpointName = settings.EndpointName();
            var metricsContext = new MetricsContext(endpointName);
            container.ConfigureComponent(() => metricsContext, DependencyLifecycle.SingleInstance);
            QueueLengthTracker.SetUp(metricsContext, context);

            context.Pipeline.Register<ServiceControlMonitoringRegistration>();

            var hostInformation = new HostInformation(
                settings.Get<Guid>("NServiceBus.HostInformation.HostId"),
                settings.Get<string>("NServiceBus.HostInformation.DisplayName"),
                settings.Get<Dictionary<string, string>>("NServiceBus.HostInformation.Properties"));


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

            container.ConfigureComponent<ServiceControlReporting>(DependencyLifecycle.SingleInstance)
                .ConfigureProperty(task => task.HeaderValues, headers);

            RegisterStartupTask<ReportingStartupTask>();
            RegisterStartupTask<ServiceControlReporting>();


            var queueLengthReport = new QueueLengthReport();
            var queueLengthReporter = new NativeQueueLengthReporter(queueLengthReport);
            queueLengthReporter.ReportQueueLength(settings.LocalAddress().ToString(), null);
            container.ConfigureComponent<IReportNativeQueueLengths>(() => queueLengthReporter, DependencyLifecycle.SingleInstance);
            container.ConfigureComponent<PeriodicallySendQueueDataToServiceControl>(DependencyLifecycle.SingleInstance)
                .ConfigureProperty(task => task.HeaderValues, headers);

            RegisterStartupTask<PeriodicallySendQueueDataToServiceControl>();
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

        class ErrorObserver : IObserver<FirstLevelRetry>, IObserver<SecondLevelRetry>
        {
            readonly Action<string> onMessageTypeRetry;

            public ErrorObserver(Action<string> onMessageTypeRetry)
            {
                this.onMessageTypeRetry = onMessageTypeRetry;
            }

            public void OnNext(FirstLevelRetry value) => Report(value.Headers);
            public void OnNext(SecondLevelRetry value) => Report(value.Headers);

            void Report(Dictionary<string, string> headers)
            {
                if (headers.TryGetValue(Headers.EnclosedMessageTypes, out var msgType))
                {
                    onMessageTypeRetry(msgType);
                }
            }

            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }

        class ServiceControlMonitoringRegistration : RegisterStep
        {
            public ServiceControlMonitoringRegistration()
                : base("InvokeMetrics", typeof(ServiceControlMonitoringRegistrationBehavior), "Invokes ServiceControlMonitoring logic")
            {
                InsertBefore(WellKnownStep.ProcessingStatistics);
            }
        }

        class ServiceControlReporting : FeatureStartupTask
        {
            public ServiceControlReporting(MetricsContext metricsContext, ISendMessages dispatcher, ReportingOptions options)
            {
                this.metricsContext = metricsContext;
                this.dispatcher = dispatcher;
                this.options = options;
            }

            protected override void OnStart()
            {
                HeaderValues.Add(Headers.EnclosedMessageTypes, "NServiceBus.Metrics.MetricReport");
                HeaderValues.Add(Headers.ContentType, ContentTypes.Json);

                var serviceControlReport = new NServiceBusMetricReport(dispatcher, options, HeaderValues, metricsContext);

                task = Task.Run(async () =>
                {
                    while (cancellationTokenSource.IsCancellationRequested == false)
                    {
                        serviceControlReport.RunReport();
                        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    }
                });
            }

            protected override void OnStop()
            {
                cancellationTokenSource.Cancel();
                task.GetAwaiter().GetResult();
            }

            readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            readonly MetricsContext metricsContext;
            readonly ISendMessages dispatcher;
            readonly ReportingOptions options;
            public Dictionary<string, string> HeaderValues { get; set; }
            Task task;
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
                    BuildReporter("CriticalTime", buffers.CriticalTime),
                    BuildReporter("Retries", buffers.Retries),
                    BuildReporter("QueueLength", buffers.QueueLength)
                };
            }

            RawDataReporter BuildReporter(string metricType, Buffer buffer)
            {
                var reporter = new RawDataReporter(BuildSend(CreateHeaders(metricType), options.TimeToBeReceived), buffer.Ring, (entries, binaryWriter) => buffer.Writer.Write(binaryWriter, entries));
                reporter.Start();
                return reporter;
            }

            protected override void OnStop()
            {
                Task.WhenAll(reporters.Select(r => r.Stop())).GetAwaiter().GetResult();
            }

            Func<byte[], Task> BuildSend(Dictionary<string, string> headers, TimeSpan timeToBeReceived)
            {
                var completed = Task.FromResult(0);
                return body =>
                {
                    var operation = new TransportMessage(Guid.NewGuid().ToString(), headers)
                    {
                        Body = body,
                        MessageIntent = MessageIntentEnum.Send,
                        
                        // TTBR is copied to the TransportMessage by the infrastructure before it hits. If ISendMessages is called manually, it needs to be passed in here
                        TimeToBeReceived = timeToBeReceived,
                    };
                    try
                    {
                        dispatcher.Send(operation, new SendOptions(options.ServiceControlMetricsAddress)
                        {
                            EnlistInReceiveTransaction = false,
                        });
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