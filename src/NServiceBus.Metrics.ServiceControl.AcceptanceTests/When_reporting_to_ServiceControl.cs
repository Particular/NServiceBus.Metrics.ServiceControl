namespace NServiceBus.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Metrics;
using EndpointTemplates;
using NUnit.Framework;

public class When_reporting_to_ServiceControl : NServiceBusAcceptanceTest
{
    static string MonitoringSpyAddress => Conventions.EndpointNamingConvention(typeof(MonitoringSpy));
    static readonly Guid HostId = Guid.NewGuid();

    [Test]
    public async Task Should_send_metadata_to_configured_queue()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Sender>()
            .WithEndpoint<MonitoringSpy>()
            .Done(c => c.Report != null)
            .Run()
            .ConfigureAwait(false);

        Assert.That(context.Report, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Report.PluginVersion, Is.EqualTo(3));
            Assert.That(context.Report.LocalAddress, Is.Not.Empty);

            Assert.That(context.Headers[Headers.OriginatingHostId], Is.EqualTo(HostId.ToString("N")));
            Assert.That(context.Headers[Headers.EnclosedMessageTypes], Is.EqualTo("NServiceBus.Metrics.EndpointMetadataReport"));
            Assert.That(context.Headers[Headers.ContentType], Is.EqualTo(ContentTypes.Json));
        }
    }

    public class Context : ScenarioContext
    {
        public EndpointMetadataReport Report { get; set; }

        public IReadOnlyDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }

    class Sender : EndpointConfigurationBuilder
    {
        public Sender()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.UniquelyIdentifyRunningInstance().UsingCustomIdentifier(HostId);
                c.EnableMetrics().SendMetricDataToServiceControl(MonitoringSpyAddress, TimeSpan.FromSeconds(1));
            });
        }
    }

    public class MonitoringSpy : EndpointConfigurationBuilder
    {
        public MonitoringSpy() =>
            EndpointSetup<DefaultServer>(c =>
            {
                c.UseSerialization<SystemJsonSerializer>();
                c.LimitMessageProcessingConcurrencyTo(1);
            }).IncludeType<EndpointMetadataReport>();

        [Handler]
        public class MetricHandler(Context testContext) : IHandleMessages<EndpointMetadataReport>
        {
            public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
            {
                testContext.Report = message;
                testContext.Headers = context.MessageHeaders;

                return Task.CompletedTask;
            }
        }
    }
}