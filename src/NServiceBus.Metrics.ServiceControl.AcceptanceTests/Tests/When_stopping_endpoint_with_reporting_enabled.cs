namespace NServiceBus.Metrics.AcceptanceTests
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_stopping_endpoint_with_reporting_enabled : NServiceBusAcceptanceTest
    {
        static string MonitoringSpyAddress => Conventions.EndpointNamingConvention(typeof(MonitoringSpy));
        static TimeSpan SendInterval = TimeSpan.FromSeconds(30);

        [Test]
        public async Task Should_not_delay_endpoint_stop()
        {
            var stopWatch = Stopwatch.StartNew();
            
            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<Sender>()
                .WithEndpoint<MonitoringSpy>()
                .Done(c => c.EndpointsStarted)
                .Run()
                .ConfigureAwait(false);
            
            stopWatch.Stop();
            
            Assert.That(stopWatch.Elapsed, Is.LessThan(SendInterval));
        }
        
        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(MonitoringSpyAddress, SendInterval);
                });
            }
        }

        class MonitoringSpy : EndpointConfigurationBuilder
        {
            public MonitoringSpy()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseSerialization<NewtonsoftJsonSerializer>();
                    c.LimitMessageProcessingConcurrencyTo(1);
                }).IncludeType<EndpointMetadataReport>();
            }

            public class MetricHandler : IHandleMessages<EndpointMetadataReport>
            {
                public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }
    }
}