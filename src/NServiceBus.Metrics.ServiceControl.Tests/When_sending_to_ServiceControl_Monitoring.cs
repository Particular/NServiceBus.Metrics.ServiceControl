namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using System.Threading;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;
    using Pipeline.Contexts;

    public class When_sending_to_ServiceControl_Monitoring : NServiceBusAcceptanceTest
    {
        static readonly string ServiceControlMetricsAddress = Conventions.EndpointNamingConvention(typeof(MonitoringMock));
        const string CustomInstanceId = "my-custom-instance";

        [Test]
        public void Should_report()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                        bus.SendLocal(new MyMessage());
                    }
                }))
                .WithEndpoint<MonitoringMock>()
                .Done(c => c.GotReport)
                .Run();

            Assert.True(context.GotReport, "Should have received report");
        }

        public class Context : ScenarioContext
        {
            public bool GotReport { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg => cfg.EnableReporting().SendMetricDataToServiceControl(ServiceControlMetricsAddress, CustomInstanceId));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public void Handle(MyMessage message)
                {
                    Thread.Sleep(1000);
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
                    //var transportMessage = context.PhysicalMessage;
                    Context.GotReport = true;
                }
            }
        }

        public class MyMessage : IMessage
        {}
    }
}