namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Features;
    using Hosting;
    using Pipeline;
    using Routing;

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

            var messageSourceKeyFactory = new MessageSourceKeyFactory(featureContext.Settings.EndpointName(), Guid.NewGuid().ToString());

            var pipeline = featureContext.Pipeline;

            pipeline.Register(b => new DispatchQueueLengthBehavior(queueLengthTracker, messageSourceKeyFactory), nameof(DispatchQueueLengthBehavior));

            pipeline.Register(new IncomingQueueLengthBehavior(queueLengthTracker, featureContext.Settings.LocalAddress()), nameof(IncomingQueueLengthBehavior));
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

        class DispatchQueueLengthBehavior : IBehavior<IDispatchContext, IDispatchContext>
        {
            readonly QueueLengthTracker queueLengthTracker;
            readonly MessageSourceKeyFactory messageSourceKeyFactory;

            public DispatchQueueLengthBehavior(QueueLengthTracker queueLengthTracker, MessageSourceKeyFactory messageSourceKeyFactory)
            {
                this.queueLengthTracker = queueLengthTracker;
                this.messageSourceKeyFactory = messageSourceKeyFactory;
            }

            public Task Invoke(IDispatchContext context, Func<IDispatchContext, Task> next)
            {
                foreach (var transportOperation in context.Operations)
                {
                    var key = messageSourceKeyFactory.BuildKey(transportOperation.AddressTag);
                    var sequence = queueLengthTracker.RegisterSend(key);

                    transportOperation.Message.Headers[KeyHeaderName] = key;
                    transportOperation.Message.Headers[ValueHeaderName] = sequence.ToString();
                }
                return next(context);
            }
        }

        class MessageSourceKeyFactory
        {
            readonly string stableKey;
            readonly string instanceKey;

            public MessageSourceKeyFactory(string stableKey, string instanceKey)
            {
                this.stableKey = stableKey;
                this.instanceKey = instanceKey;
            }

            public string BuildKey(AddressTag addressTag)
            {
                if (addressTag is UnicastAddressTag unicast)
                {
                    return BuildKey(unicast.Destination);
                }

                if (addressTag is MulticastAddressTag multicast)
                {
                    return BuildKey(multicast.MessageType.AssemblyQualifiedName);
                }

                throw new Exception("Not supported address tag");
            }

            string BuildKey(string destination)
            {
                return $"{destination}-{stableKey};{instanceKey}".ToLowerInvariant();
            }
        }

        class IncomingQueueLengthBehavior : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
        {
            readonly QueueLengthTracker queueLengthTracker;
            readonly string inputQueue;

            public IncomingQueueLengthBehavior(QueueLengthTracker queueLengthTracker, string inputQueue)
            {
                this.queueLengthTracker = queueLengthTracker;
                this.inputQueue = inputQueue;
            }

            public Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
            {
                if (context.Headers.TryGetValue(KeyHeaderName, out var key) && context.Headers.TryGetValue(ValueHeaderName, out var value))
                {
                    if (long.TryParse(value, out var sequence))
                    {
                        queueLengthTracker.RegisterReceive(key, sequence, inputQueue);
                    }
                }
                return next(context);
            }
        }
    }
}
