namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_reporting_to_ServiceControl_expires : NServiceBusAcceptanceTest
    {
        static string MonitoringSpyAddress => Conventions.EndpointNamingConvention(typeof(MonitoringMock));
        static Guid HostId = Guid.NewGuid();
        static readonly TimeSpan TTBR = TimeSpan.FromSeconds(10);

        [Test]
        public async Task Should_report_nothing_when_ttbr_breached()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(s=>s.SendLocal(new MyMessage())))
                .Done(ctx => ctx.MessageProcessedBySender)
                .Run();

            await Task.Delay(TTBR + TTBR);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<MonitoringMock>()
                .Run(TimeSpan.FromSeconds(10));

            Assert.IsFalse(context.WasCalled);
        }

        [Test]
        public async Task Should_report_when_ttbr_not_breached()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(s => s.SendLocal(new MyMessage())))
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

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UniquelyIdentifyRunningInstance().UsingCustomIdentifier(HostId);
                    var metrics = c.EnableMetrics();
                    metrics.SendMetricDataToServiceControl(MonitoringSpyAddress, TimeSpan.FromSeconds(1));
                    metrics.SetServiceControlMetricsMessageTTBR(TTBR);
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                Context testContext;

                public MyMessageHandler(Context context)
                {
                    testContext = context;
                }

                public async Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    await Task.Delay(100);
                    testContext.MessageProcessedBySender = true;
                }
            }
        }

        public class MonitoringMock : EndpointConfigurationBuilder
        {
            public MonitoringMock()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseSerialization<NewtonsoftSerializer>();
                    c.LimitMessageProcessingConcurrencyTo(1);
                }).IncludeType<EndpointMetadataReport>();
            }

            public class MetricHandler : IHandleMessages<EndpointMetadataReport>
            {
                Context testContext;

                public MetricHandler(Context context)
                {
                    testContext = context;
                }

                public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
                {
                    testContext.WasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : IMessage
        { }
    }
}