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
            var tracker = new QueueLengthTracker(metricsContext);

            var pipeline = featureContext.Pipeline;
            var container = featureContext.Container;

            //Use HostId as a stable session ID
            var hostId = featureContext.Settings.Get<Guid>("NServiceBus.HostInformation.HostId");
            var localAddress = featureContext.Settings.LocalAddress();

            container.ConfigureComponent<DispatchQueueLengthBehavior>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(b => b.SessionId, hostId)
                .ConfigureProperty(b => b.LengthTracker, tracker);

            container.ConfigureComponent<IncomingQueueLengthBehavior>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(b => b.LengthTracker, tracker)
                .ConfigureProperty(b => b.InputQueue, localAddress);

            container.ConfigureComponent(() => tracker, DependencyLifecycle.SingleInstance);

            pipeline.Register<IncomingQueueLengthBehavior.Registration>();
            pipeline.Register<DispatchQueueLengthBehavior.Registration>();
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

        class DispatchQueueLengthBehavior : IBehavior<OutgoingContext>
        {
            public QueueLengthTracker LengthTracker { get; set; }
            public Guid SessionId { get; set; }

            public void Invoke(OutgoingContext context, Action next)
            {
                var options = context.DeliveryOptions;
                long sequence;
                string key;

                if (options is PublishOptions publishOptions)
                {
                    key = BuildKey(publishOptions.EventType.AssemblyQualifiedName);
                    sequence = LengthTracker.RegisterSend(key);
                }
                else if (options is SendOptions sendOptions)
                {
                    key = BuildKey(sendOptions.Destination.ToString());
                    sequence = LengthTracker.RegisterSend(key);
                }
                else
                {
                    throw new Exception($"Not recognized delivery options of type: `{options.GetType()}`");
                }

                context.OutgoingMessage.Headers[KeyHeaderName] = key;
                context.OutgoingMessage.Headers[ValueHeaderName] = sequence.ToString();

                next();
            }

            string BuildKey(string keyBase)
            {
                return $"{keyBase}-{SessionId}".ToLowerInvariant();
            }

            public class Registration : RegisterStep
            {
                public Registration()
                    : base(typeof(DispatchQueueLengthBehavior).Name, typeof(DispatchQueueLengthBehavior), "Enhances messages being sent with queue length headers")
                {
                    InsertBefore(WellKnownStep.DispatchMessageToTransport);
                }
            }
        }

        class IncomingQueueLengthBehavior : IBehavior<IncomingContext>
        {
            public QueueLengthTracker LengthTracker { get; set; }
            public Address InputQueue { get; set; }

            public void Invoke(IncomingContext context, Action next)
            {
                var msg = context.IncomingLogicalMessage;

                if (msg.Headers.TryGetValue(KeyHeaderName, out var key) && msg.Headers.TryGetValue(ValueHeaderName, out var value))
                {
                    if (long.TryParse(value, out var sequence))
                    {
                        LengthTracker.RegisterReceive(key, sequence, InputQueue.ToString());
                    }
                }

                next();
            }

            public class Registration : RegisterStep
            {
                public Registration()
                    : base(typeof(IncomingQueueLengthBehavior).Name, typeof(IncomingQueueLengthBehavior), "Reports incoming message queue length headers")
                {
                    InsertAfter(WellKnownStep.ExecuteLogicalMessages);
                }
            }

        }
    }
}
