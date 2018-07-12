using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Features;
using NServiceBus.Metrics;
using NServiceBus.Metrics.ServiceControl;
using NServiceBus.Pipeline;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

public class When_native_queue_length_is_reported : NServiceBusAcceptanceTest
{
    static string queueName = "queue";
    static readonly byte[] QueueNameBytes = new UTF8Encoding(false).GetBytes(queueName);

    [Test]
    public async Task Should_sent_reported_values_to_ServiceControl()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithNativeQueueLengthSupport>()
            .WithEndpoint<MonitoringSpy>()
            .Done(c => c.ReportReceived)
            .Run(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.IsTrue(ContainsPattern(context.ReportBody, QueueNameBytes));
        Assert.IsTrue(ContainsPattern(context.ReportBody, new []{ (byte)10 }));
    }

    class Context : ScenarioContext
    {
        public bool ReportReceived { get; set; }
        public byte[] ReportBody { get; set; }
    }

    class EndpointWithNativeQueueLengthSupport : EndpointConfigurationBuilder
    {
        public EndpointWithNativeQueueLengthSupport()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var monitoringSpyAddress = Conventions.EndpointNamingConvention(typeof(MonitoringSpy));

                c.EnableMetrics().SendMetricDataToServiceControl(monitoringSpyAddress, TimeSpan.FromSeconds(1));
                c.EnableFeature<NativeQueueLengthFeature>();
            });
        }

        class NativeQueueLengthFeature : Feature
        {
            public NativeQueueLengthFeature()
            {
                DependsOn("NServiceBus.Metrics.ServiceControl.ReportingFeature");
            }

            protected override void Setup(FeatureConfigurationContext context)
            {
                context.RegisterStartupTask(b => new NativeQueueLengthReporter(b.Build<IReportNativeQueueLength>()));
            }
        }

        class NativeQueueLengthReporter : FeatureStartupTask
        {
            IReportNativeQueueLength queueLengthReporter;

            public NativeQueueLengthReporter(IReportNativeQueueLength queueLengthReporter)
            {
                this.queueLengthReporter = queueLengthReporter;
            }

            protected override Task OnStart(IMessageSession session)
            {
                queueLengthReporter.ReportQueueLength(queueName, 10);

                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.FromResult(0);
            }
        }
    }


    class MonitoringSpy : EndpointConfigurationBuilder
    {
        public MonitoringSpy()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.Pipeline.Register(typeof(RawMessageBehavior), "Transport message handler.");
                c.UseSerialization<JsonSerializer>();
                c.LimitMessageProcessingConcurrencyTo(1);
            }).IncludeType<EndpointMetadataReport>();
        }

        public class RawMessageBehavior : Behavior<IIncomingPhysicalMessageContext>
        {
            public Context TestContext { get; set; }

            public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                if (context.MessageHeaders.TryGetValue("NServiceBus.Metric.Type", out var metricType) && metricType == "QueueLength")
                {
                    TestContext.ReportReceived = true;
                    TestContext.ReportBody = context.Message.Body.ToArray();
                }

                return Task.FromResult(0);
            }
        }
    }
}