namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using System.Threading;
    using AcceptanceTesting;
    using Config;
    using Config.ConfigurationSource;
    using EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;
    using Pipeline.Contexts;

    public class When_reporting_to_ServiceControl_expires : NServiceBusAcceptanceTest
    {
        static readonly TimeSpan TTBR = TimeSpan.FromSeconds(10);

        [Test]
        public void Should_report_nothing_when_ttbr_breached()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.SendLocal(new MyMessage());
                }))
                .Done(ctx => ctx.MessageProcessedBySender)
                .Run();

            Thread.Sleep(TTBR + TTBR);

            Scenario.Define(context)
                .WithEndpoint<MonitoringMock>()
                .Done(ctx => ctx.WasCalled)
                .Run(TimeSpan.FromSeconds(10));

            Assert.IsFalse(context.WasCalled);
        }

        [Test]
        public void Should_report_when_ttbr_not_breached()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.SendLocal(new MyMessage());
                }))
                .WithEndpoint<MonitoringMock>()
                .Done(ctx => ctx.WasCalled)
                .Run();

            Assert.True(context.WasCalled);
        }

        public class Context : ScenarioContext
        {
            public bool MessageProcessedBySender { get; set; }
            public bool WasCalled { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    var monitoringQueue = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(MonitoringMock));

                    cfg.SendMetricDataToServiceControl(monitoringQueue, "my-custom-instance");
                    cfg.SetServiceControlTTBR(TTBR);
                    cfg.Transactions().Disable(); //transactional msmq with ttbr not supported
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public void Handle(MyMessage message)
                {
                    Thread.Sleep(100);

                    Context.MessageProcessedBySender = true;
                }
            }
        }

        public class MonitoringMock : EndpointConfigurationBuilder
        {
            public MonitoringMock()
            {
                EndpointSetup<DefaultServer>();
            }

            class MyOverride : INeedInitialization
            {
                public void Customize(BusConfiguration configuration)
                {
                    configuration.Pipeline.Replace(WellKnownStep.DeserializeMessages, typeof(MyRawMessageHandler));
                }
            }

            public class LimitConcurrency : IProvideConfiguration<TransportConfig>
            {
                public TransportConfig GetConfiguration() => new TransportConfig { MaximumConcurrencyLevel = 1 };
            }

            class MyRawMessageHandler : IBehavior<IncomingContext>
            {
                public Context Context { get; set; }

                public void Invoke(IncomingContext context, Action next)
                {
                    Context.WasCalled = true;
                }
            }
        }

        public class MyMessage : IMessage
        { }

        public class SlowMessage : IMessage
        { }
    }
}