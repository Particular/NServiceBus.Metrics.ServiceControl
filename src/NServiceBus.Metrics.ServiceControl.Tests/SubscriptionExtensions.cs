namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using AcceptanceTesting;
    using Pipeline;
    using Pipeline.Contexts;

    public static class SubscriptionExtensions
    {
        public static void OnEndpointSubscribed<TContext>(this BusConfiguration configuration, Action<SubscriptionEventArgs, TContext> action)
        where TContext : ScenarioContext
        {
            configuration.RegisterComponents(components =>
            {
                components.ConfigureComponent(() => action, DependencyLifecycle.SingleInstance);
            });
            configuration.Pipeline.Register<SubscriptionBehavior<TContext>.Registration>();
        }

        class SubscriptionBehavior<TContext> : IBehavior<IncomingContext>
            where TContext : ScenarioContext
        {
            public SubscriptionBehavior(Action<SubscriptionEventArgs, TContext> action, TContext scenarioContext)
            {
                this.action = action;
                this.scenarioContext = scenarioContext;
            }

            public void Invoke(IncomingContext context, Action next)
            {
                next();

                var msg = context.PhysicalMessage;
                var headers = msg.Headers;

                var subscriptionMessageType = GetSubscriptionMessageTypeFrom(msg);
                if (subscriptionMessageType != null)
                {
                    var intent = (MessageIntentEnum)Enum.Parse(typeof(MessageIntentEnum), headers[Headers.MessageIntent], true);
                    if (intent != MessageIntentEnum.Subscribe)
                    {
                        return;
                    }

                    if (headers.TryGetValue("Headers.SubscriberTransportAddress", out var returnAddress) == false)
                    {
                        headers.TryGetValue(Headers.ReplyToAddress, out returnAddress);
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

            public class Registration : RegisterStep
            {
                public Registration()
                    : base(nameof(SubscriptionBehavior<TContext>), typeof(SubscriptionBehavior<TContext>), nameof(SubscriptionBehavior<TContext>))
                {
                    InsertAfter(WellKnownStep.MutateIncomingTransportMessage);
                }
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