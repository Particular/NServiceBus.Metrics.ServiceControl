namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using System.Threading;
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
        static readonly TimeSpan TTBR = TimeSpan.FromSeconds(5);

        [Test]
        public async Task Should_report_nothing_when_ttbr_breached()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(s => s.SendLocal(new MyMessage())))
                .Run();

            await Task.Delay(TTBR + TTBR);

            using var tokenSource = new CancellationTokenSource();

            tokenSource.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<MonitoringMock>()
                    .Run(tokenSource.Token);

                Assert.Fail("Should have thrown exception");
            }
#pragma warning disable PS0020
            catch (TaskCanceledException)
#pragma warning restore PS0020
            {
            }
        }

        [Test]
        public async Task Should_report_when_ttbr_not_breached() =>
            await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(s => s.SendLocal(new MyMessage())))
                .WithEndpoint<MonitoringMock>()
                .Run();

        public class Context : ScenarioContext;

        class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UniquelyIdentifyRunningInstance().UsingCustomIdentifier(HostId);
                    var metrics = c.EnableMetrics();
                    metrics.SendMetricDataToServiceControl(MonitoringSpyAddress, TimeSpan.FromSeconds(1));
                    metrics.SetServiceControlMetricsMessageTTBR(TTBR);
                });

            public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
            {
                public async Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    await Task.Delay(100);
                    testContext.MarkAsCompleted();
                }
            }
        }

        public class MonitoringMock : EndpointConfigurationBuilder
        {
            public MonitoringMock() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseSerialization<SystemJsonSerializer>();
                    c.LimitMessageProcessingConcurrencyTo(1);
                }).IncludeType<EndpointMetadataReport>();

            public class MetricHandler(Context testContext) : IHandleMessages<EndpointMetadataReport>
            {
                public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
                {
                    testContext.MarkAsCompleted();
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : IMessage;
    }
}