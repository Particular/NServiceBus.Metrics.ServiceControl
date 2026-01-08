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

            Context context = null;
            try
            {
                context = await Scenario.Define<Context>()
                    .WithEndpoint<MonitoringMock>()
                    .Run(tokenSource.Token);
            }
#pragma warning disable PS0020
            catch (OperationCanceledException ex) when (ex.CancellationToken == tokenSource.Token && tokenSource.IsCancellationRequested)
#pragma warning restore PS0020
            {
                Assert.That(context?.WasCalled, Is.False);
            }
        }

        [Test]
        public async Task Should_report_when_ttbr_not_breached()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(s => s.SendLocal(new MyMessage())))
                .WithEndpoint<MonitoringMock>()
                .Done(ctx => ctx.WasCalled)
                .Run();

            Assert.That(context.WasCalled, Is.True);
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

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
                    testContext.WasCalled = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : IMessage;
    }
}