using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Features;
using NServiceBus.Metrics;
using NServiceBus.Metrics.ServiceControl;
using NUnit.Framework;
using ServiceControl.Monitoring.Messaging;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

public class When_native_queue_length_is_reported : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_sent_reported_values_to_ServiceControl()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithNativeQueueLengthSupport>()
            .WithEndpoint<MonitoringSpy>()
            .Run();

        Assert.That(result.Message, Is.Not.Null);
        Assert.That(result.Message.TagValue, Is.EqualTo("queue"));
        var entries = result.Message.Entries.Where(x => x.DateTicks > 0).ToArray();
        Assert.That(entries, Is.Not.Empty, "There should be some reported values");
        Assert.That(entries.Count(x => x.Value == 10), Is.EqualTo(1), "A reported value should be 10");
    }

    class Context : ScenarioContext
    {
        public TaggedLongValueOccurrence Message { get; set; }
    }

    class EndpointWithNativeQueueLengthSupport : EndpointConfigurationBuilder
    {
        public EndpointWithNativeQueueLengthSupport() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var monitoringSpyAddress = Conventions.EndpointNamingConvention(typeof(MonitoringSpy));

                c.EnableMetrics().SendMetricDataToServiceControl(monitoringSpyAddress, TimeSpan.FromSeconds(1));
                c.EnableFeature<NativeQueueLengthFeature>();
            });

        class NativeQueueLengthFeature : Feature
        {
            public NativeQueueLengthFeature() => DependsOn("NServiceBus.Metrics.ServiceControl.ReportingFeature");

            protected override void Setup(FeatureConfigurationContext context) => context.RegisterStartupTask(b => new NativeQueueLengthReporter(b.GetRequiredService<IReportNativeQueueLength>()));
        }

        class NativeQueueLengthReporter(IReportNativeQueueLength queueLengthReporter) : FeatureStartupTask
        {
            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                queueLengthReporter.ReportQueueLength("queue", 10);

                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }


    class MonitoringSpy : EndpointConfigurationBuilder
    {
        public MonitoringSpy() =>
            EndpointSetup<DefaultServer>(c =>
            {
                c.UseSerialization<SystemJsonSerializer>();
                c.AddDeserializer<TaggedLongValueWriterOccurrenceSerializerDefinition>();
                c.LimitMessageProcessingConcurrencyTo(1);
            }).IncludeType<EndpointMetadataReport>().IncludeType<TaggedLongValueOccurrence>();

        class MessageHandler(Context testContext) : IHandleMessages<TaggedLongValueOccurrence>, IHandleMessages<EndpointMetadataReport>
        {
            public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
            {
                if (context.MessageHeaders.TryGetValue("NServiceBus.Metric.Type", out var metricType) && metricType == "QueueLength")
                {
                    testContext.Message = message;
                    testContext.MarkAsCompleted();
                }

                return Task.CompletedTask;
            }

            public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context) => Task.CompletedTask;
        }
    }
}