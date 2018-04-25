using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using EndpointTemplates;
    using Features;
    using Pipeline;
    using Pipeline.Contexts;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_native_queue_legth_is_reproted : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_send_reported_values_to_ServiceControl()
        {
            Scenario.Define<Context>()
                .WithEndpoint<EndpointWithNativeQueueLengthSupport>()
                .WithEndpoint<MonitoringSpy>()
                .Done(c => c.QueueLengthReportReceived)
                .Run();
        }

        class Context : ScenarioContext
        {
            public bool QueueLengthReportReceived { get; set; }
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
                    queueLengthReporter.ReportQueueLength("queue", 10);
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
                        Context.QueueLengthReportReceived = true;
                    }
                }
            }
        }
    }

    
}