using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Metrics.ServiceControl.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Text;
    using EndpointTemplates;
    using Features;
    using Pipeline;
    using Pipeline.Contexts;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_native_queue_length_is_reported : NServiceBusAcceptanceTest
    {
        static string queueName = "queue";
        static readonly byte[] QueueNameBytes = new UTF8Encoding(false).GetBytes(queueName);

        [Test]
        public void Should_send_reported_values_to_ServiceControl()
        {
            var context = Scenario.Define<Context>()
                .WithEndpoint<EndpointWithNativeQueueLengthSupport>()
                .WithEndpoint<MonitoringSpy>()
                .Done(c => c.ReportReceived)
                .Run();

            Assert.IsTrue(ContainsPattern(context.ReportBody, QueueNameBytes));
            Assert.IsTrue(ContainsPattern(context.ReportBody, new [] { (byte)10 }));
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

                    c.SendMetricDataToServiceControl(monitoringSpyAddress);
                    c.EnableFeature<NativeQueueLengthFeature>();
                });
            }

            class NativeQueueLengthFeature : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    RegisterStartupTask<NativeQueueLengthReporter>();
                }
            }

            class NativeQueueLengthReporter : FeatureStartupTask
            {
                readonly IReportNativeQueueLength queueLengthReporter;

                public NativeQueueLengthReporter(IReportNativeQueueLength queueLengthReporter)
                {
                    this.queueLengthReporter = queueLengthReporter;
                }

                protected override void OnStart()
                {
                    queueLengthReporter.ReportQueueLength(queueName, 10);
                }
            }
        }

        public class MonitoringSpy : EndpointConfigurationBuilder
        {
            public MonitoringSpy()
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
                    var msg = context.PhysicalMessage;

                    if (msg.Headers.TryGetValue("NServiceBus.Metric.Type", out var metric) && metric == "QueueLength")
                    {
                        Context.ReportReceived = true;
                        Context.ReportBody = context.PhysicalMessage.Body.ToArray();
                    }
                }
            }
        }
    }

    
}