using System;
using System.Linq;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Config;
    using Config.ConfigurationSource;
    using EndpointTemplates;
    using Newtonsoft.Json.Linq;

    public class When_sending_message : NServiceBusAcceptanceTest
    {
        static string ReceiverAddress1 => AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Receiver1));
        static string ReceiverAddress2 => AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Receiver2));

        protected class QueueLengthContext : ScenarioContext
        {
            public MetricsContext Data { get; set; }
            public Func<bool> TrackReports = () => true;
        }

        [Test]
        public void Should_enhance_it_with_queue_length_properties()
        {
            var context = Scenario.Define<Context>()
                .WithEndpoint<Sender>(c =>
                {
                    c.When(s =>
                    {
                        var a1 = Address.Parse(ReceiverAddress1);
                        s.Send(a1, new TestMessage());
                        s.Send(a1, new TestMessage());

                        var a2 = Address.Parse(ReceiverAddress2);
                        s.Send(a2,new TestMessage());
                        s.Send(a2,new TestMessage());
                    });
                })
                .WithEndpoint<Receiver1>()
                .WithEndpoint<Receiver2>()
                .WithEndpoint<MonitoringSpy>()
                .Done(c => c.Headers1.Count == 2 && c.Headers2.Count == 2 && c.Data != null)
                .Run();

            AssertSequencesReported(context);
        }

        static void AssertSequencesReported(Context context)
        {
            var sessionIds = new[]
            {
                AssertHeaders(context.Headers1),
                AssertHeaders(context.Headers2)
            };

            var data = context.Data;
            var counters = data.Counters;
            var counterTokens = counters.Where(c => c.Name.StartsWith("Sent sequence for"));

            foreach (var counter in counterTokens)
            {
                var tags = counter.Tags;
                var counterBasedKey = tags.GetTagValue("key");
                var type = tags.GetTagValue("type");

                CollectionAssert.Contains(sessionIds, counterBasedKey);
                Assert.AreEqual(2, counter.Count);
                Assert.AreEqual("queue-length.sent", type);
            }
        }

        static string AssertHeaders(IProducerConsumerCollection<IDictionary<string, string>> oneReceiverHeaders)
        {
            const string keyHeader = "NServiceBus.Metrics.QueueLength.Key";
            const string valueHeader = "NServiceBus.Metrics.QueueLength.Value";

            var headers = oneReceiverHeaders.ToArray();

            var sessionKey1 = headers[0][keyHeader];
            var sessionKey2 = headers[1][keyHeader];
            Assert.AreEqual(sessionKey1, sessionKey2, "expected sessionKey1 == sessionKey2");

            var sequence1 = long.Parse(headers[0][valueHeader]);
            var sequence2 = long.Parse(headers[1][valueHeader]);
            Assert.AreNotEqual(sequence2, sequence1, "expected sequence1 != sequence2");
            Assert.IsTrue(sequence1 == 1 || sequence2 == 1, "sequence1 == 1 || sequence2 == 1");
            Assert.IsTrue(sequence1 == 2 || sequence2 == 2, "sequence1 == 2 || sequence2 == 2");
            return sessionKey1;
        }

        static readonly Guid HostId = Guid.NewGuid();

        class Context : QueueLengthContext
        {
            public Context()
            {
                TrackReports = () => Headers1.Count == 2 && Headers2.Count == 2;
            }

            public ConcurrentQueue<IDictionary<string, string>> Headers1 { get; } = new ConcurrentQueue<IDictionary<string, string>>();
            public ConcurrentQueue<IDictionary<string, string>> Headers2 { get; } = new ConcurrentQueue<IDictionary<string, string>>();
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var runningInstance = c.UniquelyIdentifyRunningInstance();
                    runningInstance.UsingCustomIdentifier(HostId);
                    
                    var address = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(MonitoringSpy));
                    c.SendMetricDataToServiceControl(address);
                });
            }
        }

        class Receiver1 : EndpointConfigurationBuilder
        {
            public Receiver1()
            {
                EndpointSetup<DefaultServer>();
            }

            public class TestMessageHandler : IHandleMessages<TestMessage>
            {
                public Context TestContext { get; set; }

                public IBus Bus { get; set; }

                public void Handle(TestMessage message)
                {
                    TestContext.Headers1.Enqueue(Bus.CurrentMessageContext.Headers);
                }
            }

            public class LimitConcurrency : IProvideConfiguration<TransportConfig>
            {
                public TransportConfig GetConfiguration() => new TransportConfig { MaximumConcurrencyLevel = 1 };
            }
        }

        protected class MonitoringSpy : EndpointConfigurationBuilder
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

        class Receiver2 : EndpointConfigurationBuilder
        {
            public Receiver2()
            {
                EndpointSetup<DefaultServer>();
            }

            public class TestMessageHandler : IHandleMessages<TestMessage>
            {
                public Context TestContext { get; set; }

                public IBus Bus { get; set; }

                public void Handle(TestMessage message)
                {
                    TestContext.Headers2.Enqueue(Bus.CurrentMessageContext.Headers);
                }
            }

            public class LimitConcurrency : IProvideConfiguration<TransportConfig>
            {
                public TransportConfig GetConfiguration() => new TransportConfig { MaximumConcurrencyLevel = 1 };
            }
        }

        public class TestMessage : ICommand
        {
        }
    }
}