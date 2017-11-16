namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    using System;
    using System.Collections.Concurrent;
    using Features;
    using Pipeline;
    using Pipeline.Contexts;
    using Unicast;

    class QueueLengthTracker
    {
        const string KeyHeaderName = "NServiceBus.Metrics.QueueLength.Key";
        const string ValueHeaderName = "NServiceBus.Metrics.QueueLength.Value";

        MetricsContext metricsContext;

        ConcurrentDictionary<string, Counter> sendingCounters = new ConcurrentDictionary<string, Counter>();
        ConcurrentDictionary<string, Gauge> receivingReporters = new ConcurrentDictionary<string, Gauge>();

        QueueLengthTracker(MetricsContext metricsContext)
        {
            this.metricsContext = metricsContext;
        }

        public static void SetUp(MetricsContext metricsContext, FeatureConfigurationContext featureContext)
        {
            var queueLengthTracker = new QueueLengthTracker(metricsContext);

            var pipeline = featureContext.Pipeline;
            var container = featureContext.Container;

            //Use HostId as a stable session ID
            var hostId = featureContext.Settings.Get<Guid>("NServiceBus.HostInformation.HostId");
            var localAddress = featureContext.Settings.LocalAddress();
            container.ConfigureComponent(b => new DispatchQueueLengthBehavior(queueLengthTracker, hostId), DependencyLifecycle.InstancePerCall);
            container.ConfigureComponent(b => new IncomingQueueLengthBehavior(queueLengthTracker, localAddress), DependencyLifecycle.InstancePerCall);

            pipeline.Register< IncomingQueueLengthRegisterStep>();
            pipeline.Register< DispatchRegisterStep>();

            pipeline.Register(nameof(DispatchQueueLengthBehavior), typeof(DispatchQueueLengthBehavior), "Enhances messages being sent with queue length headers");
            pipeline.Register(nameof(IncomingQueueLengthBehavior), typeof(IncomingQueueLengthBehavior), "Reports incoming message queue length headers");
        }

        long RegisterSend(string key)
        {
            var counter = sendingCounters.GetOrAdd(key, CreateSendCounter);
            return counter.Increment();
        }

        Counter CreateSendCounter(string key)
        {
            return metricsContext.Counter(key);
        }

        void RegisterReceive(string key, long sequence, string inputQueue)
        {
            var reporter = receivingReporters.GetOrAdd(key, k => CreateGauge(k, inputQueue));
            reporter.Report(sequence);
        }

        Gauge CreateGauge(string key, string inputQueue)
        {
            return metricsContext.Gauge(key, inputQueue);
        }

        class DispatchRegisterStep : RegisterStep
        {
            public DispatchRegisterStep()
                : base(typeof(DispatchQueueLengthBehavior).Name, typeof(DispatchQueueLengthBehavior), "Enhances messages being sent with queue length headers")
            {
                InsertBefore(WellKnownStep.DispatchMessageToTransport);
            }
        }

        class DispatchQueueLengthBehavior : IBehavior<OutgoingContext>
        {
            readonly QueueLengthTracker queueLengthTracker;
            readonly Guid session;

            public DispatchQueueLengthBehavior(QueueLengthTracker queueLengthTracker, Guid session)
            {
                this.queueLengthTracker = queueLengthTracker;
                this.session = session;
            }

            public void Invoke(OutgoingContext context, Action next)
            {
                var options = context.DeliveryOptions;
                long sequence;
                string key;

                if (options is PublishOptions publishOptions)
                {
                    key = BuildKey(publishOptions.EventType.AssemblyQualifiedName);
                    sequence = queueLengthTracker.RegisterSend(key);
                }
                else if (options is SendOptions sendOptions)
                {
                    key = BuildKey(sendOptions.Destination.ToString());
                    sequence = queueLengthTracker.RegisterSend(key);
                }
                else
                {
                    throw new Exception($"Not recognized delivery options of type: `{options.GetType()}`");
                }

                context.OutgoingMessage.Headers[KeyHeaderName] = key;
                context.OutgoingMessage.Headers[ValueHeaderName] = sequence.ToString();

                next();
            }

            string BuildKey(string destination)
            {
                return $"{destination}-{session}".ToLowerInvariant();
            }
        }

        class IncomingQueueLengthRegisterStep : RegisterStep
        {
            public IncomingQueueLengthRegisterStep()
                : base(typeof(IncomingQueueLengthBehavior).Name, typeof(IncomingQueueLengthBehavior), "Reports incoming message queue length headers")
            {
                InsertAfter(WellKnownStep.ExecuteLogicalMessages);
            }
        }

        class IncomingQueueLengthBehavior : IBehavior<IncomingContext>
        {
            readonly QueueLengthTracker queueLengthTracker;
            readonly Address inputQueue;

            public IncomingQueueLengthBehavior(QueueLengthTracker queueLengthTracker, Address inputQueue)
            {
                this.queueLengthTracker = queueLengthTracker;
                this.inputQueue = inputQueue;
            }

            public void Invoke(IncomingContext context, Action next)
            {
                var msg = context.IncomingLogicalMessage;

                if (msg.Headers.TryGetValue(KeyHeaderName, out var key) && msg.Headers.TryGetValue(ValueHeaderName, out var value))
                {
                    if (long.TryParse(value, out var sequence))
                    {
                        queueLengthTracker.RegisterReceive(key, sequence, inputQueue.ToString());
                    }
                }

                next();
            }
        }
    }
}
