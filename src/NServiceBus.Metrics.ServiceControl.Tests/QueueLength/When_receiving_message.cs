using System;
using System.Linq;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using Config;
    using Config.ConfigurationSource;
    using EndpointTemplates;

    public class When_receiving_message : NServiceBusAcceptanceTest
    {
        const string KeyHeader = "NServiceBus.Metrics.QueueLength.Key";
        const string ValueHeader = "NServiceBus.Metrics.QueueLength.Value";
        const double SequenceValue = 42;
        static readonly string SequenceKey = Guid.NewGuid().ToString();

        class QueueLengthContext : ScenarioContext
        {
            public MetricsContext Data { get; set; }
            public Func<bool> TrackReports = () => true;
        }

        [Test]
        public void Should_report_sequence_for_session()
        {
            var context = Scenario.Define<QueueLengthContext>()
                .WithEndpoint<Receiver>(c => c.When(s =>
                {
                    s.OutgoingHeaders[KeyHeader] = SequenceKey;
                    s.OutgoingHeaders[ValueHeader] = SequenceValue.ToString();
                    s.SendLocal(new TestMessage());
                }))
                .WithEndpoint<MonitoringSpy>()
                .Done(c => c.Data != null && c.Data.Gauges.Any(IsReceivedSequence))
                .Run();

            var data = context.Data;
            var gauges = data.Gauges;
            var gauge = gauges.Single(IsReceivedSequence);
            var tags = gauge.Tags;

            Assert.AreEqual("queue-length.received", tags.GetTagValue("type"));
            Assert.AreEqual(SequenceKey, tags.GetTagValue("key"));
            Assert.AreEqual(SequenceValue, gauge.Value);
        }

        static bool IsReceivedSequence(Gauge g) => g.Name.StartsWith("Received sequence for");

        class MonitoringSpy : EndpointConfigurationBuilder
        {
            public MonitoringSpy()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseSerialization<JsonSerializer>();
                }).IncludeType<MetricReport>();
            }

            public class MetricHandler : IHandleMessages<MetricReport>
            {
                public QueueLengthContext TestContext { get; set; }

                public void Handle(MetricReport message)
                {
                    if (TestContext.TrackReports())
                    {
                        TestContext.Data = message.Data;
                    }
                }
            }

            public class LimitConcurrency : IProvideConfiguration<TransportConfig>
            {
                public TransportConfig GetConfiguration() => new TransportConfig { MaximumConcurrencyLevel = 1 };
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var address = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(MonitoringSpy));
                    c.SendMetricDataToServiceControl(address);
                    c.Pipeline.Remove("DispatchQueueLengthBehavior");
                });
            }

            public class TestMessageHandler : IHandleMessages<TestMessage>
            {
                public void Handle(TestMessage message) { }
            }

            public class LimitConcurrency : IProvideConfiguration<TransportConfig>
            {
                public TransportConfig GetConfiguration() => new TransportConfig { MaximumConcurrencyLevel = 1 };
            }
        }

        class TestMessage : ICommand
        {
        }
    }
}