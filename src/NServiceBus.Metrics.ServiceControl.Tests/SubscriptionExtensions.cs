namespace NServiceBus.AcceptanceTests.PubSub
{
    using System;
    using System.Linq;
    using AcceptanceTesting;
    using Pipeline;
    using Pipeline.Contexts;
    using Unicast.Subscriptions;

    class SubscriptionBehavior<TContext> : IBehavior<IncomingContext> where TContext : ScenarioContext
    {
        readonly Action<SubscriptionEventArgs, TContext> action;
        readonly TContext scenarioContext;

        public SubscriptionBehavior(Action<SubscriptionEventArgs, TContext> action, TContext scenarioContext)
        {
            this.action = action;
            this.scenarioContext = scenarioContext;
        }

        public void Invoke(IncomingContext context, Action next)
        {
            next();
            var subscriptionMessageType = GetSubscriptionMessageTypeFrom(context.PhysicalMessage);
            if (subscriptionMessageType != null)
            {
                action(new SubscriptionEventArgs
                {
                    MessageType = subscriptionMessageType,
                    SubscriberReturnAddress = context.PhysicalMessage.ReplyToAddress
                }, scenarioContext);
            }
        }

        static string GetSubscriptionMessageTypeFrom(TransportMessage msg)
        {
            return (from header in msg.Headers where header.Key == Headers.SubscriptionMessageType select header.Value).FirstOrDefault();
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("SubscriptionBehavior", typeof(SubscriptionBehavior<TContext>), "So we can get subscription events")
            {
                InsertBefore(WellKnownStep.CreateChildContainer);
            }
        }
    }
}