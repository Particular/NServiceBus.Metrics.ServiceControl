namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using System.Threading;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;
    using Pipeline.Contexts;

    public class When_reporting_to_ServiceControl_expires : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_report_properly()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.SendLocal(new MyMessage());
                }))
                .Done(ctx => ctx.WasCalled)
                .WithEndpoint<MonitoringMock>()
                .Run(TimeSpan.FromSeconds(10));

            Assert.IsFalse(context.WasCalled);
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    var queue = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(MonitoringMock));
                    cfg.SendMetricDataToServiceControl(queue, "my-custom-instance");
                    cfg.SetServiceControlTTBR(TimeSpan.Zero);
                    cfg.Transactions().Disable(); //transactional msmq with ttbr not supported
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public void Handle(MyMessage message)
                {
                    Thread.Sleep(100);
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
    }
}