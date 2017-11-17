namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using Pipeline;
    using Pipeline.Contexts;

    public static class SubscriptionExtensions
    {
        public static void OnEndpointSubscribed<TContext>(this BusConfiguration configuration, Action<SubscriptionEventArgs, TContext> action)
        where TContext : ScenarioContext
        {
            configuration.Pipeline.Register("NotifySubscriptionBehavior", builder =>
            {
                var context = builder.Build<TContext>();
                return new SubscriptionBehavior<TContext>(action, context, MessageIntentEnum.Subscribe);
            }, "Provides notifications when endpoints subscribe");
        }

        public static void OnEndpointUnsubscribed<TContext>(this EndpointConfiguration configuration, Action<SubscriptionEventArgs, TContext> action)
        where TContext : ScenarioContext
        {
            configuration.Pipeline.Register("NotifyUnsubscriptionBehavior", builder =>
            {
                var context = builder.Build<TContext>();
                return new SubscriptionBehavior<TContext>(action, context, MessageIntentEnum.Unsubscribe);
            }, "Provides notifications when endpoints unsubscribe");
        }

        class SubscriptionBehavior<TContext> : IBehavior<IncomingContext> 
            where TContext : ScenarioContext
        {
            public SubscriptionBehavior(Action<SubscriptionEventArgs, TContext> action, TContext scenarioContext, MessageIntentEnum intentToHandle)
            {
                this.action = action;
                this.scenarioContext = scenarioContext;
                this.intentToHandle = intentToHandle;
            }

            public void Invoke(IncomingContext context, Action next)
            {
                next();

                var msg = context.PhysicalMessage;

                var subscriptionMessageType = GetSubscriptionMessageTypeFrom(msg);
                if (subscriptionMessageType != null)
                {
                    string returnAddress;
                    if (!msg.Headers.TryGetValue(Headers.SubscriberTransportAddress, out returnAddress))
                    {
                        msg.Headers.TryGetValue(Headers.ReplyToAddress, out returnAddress);
                    }

                    var intent = (MessageIntentEnum)Enum.Parse(typeof(MessageIntentEnum), msg.Headers[Headers.MessageIntent], true);
                    if (intent != intentToHandle)
                    {
                        return;
                    }

                    action(new SubscriptionEventArgs
                    {
                        MessageType = subscriptionMessageType,
                        SubscriberReturnAddress = returnAddress
                    }, scenarioContext);
                }
            }

            static string GetSubscriptionMessageTypeFrom(TransportMessage msg)
            {
                return msg.Headers.TryGetValue(Headers.SubscriptionMessageType, out var headerValue) ? headerValue : null;
            }

            Action<SubscriptionEventArgs, TContext> action;
            TContext scenarioContext;
            MessageIntentEnum intentToHandle;
        }

        class Registration<SubBehavior> : RegisterStep
        {
            public Registration() 
                : base(nameof(SubBehavior), typeof(SubBehavior), nameof(SubBehavior))
            {
                InsertAfter(WellKnownStep.MutateIncomingTransportMessage);
            }
        }
    }

    public class SubscriptionEventArgs
    {
        /// <summary>
        /// The address of the subscriber.
        /// </summary>
        public string SubscriberReturnAddress { get; set; }

        /// <summary>
        /// The type of message the client subscribed to.
        /// </summary>
        public string MessageType { get; set; }
    }
}