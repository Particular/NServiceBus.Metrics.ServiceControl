using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Features;
using NUnit.Framework;

namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using Config;
    using Config.ConfigurationSource;
    using EndpointTemplates;

    public class When_publishing_message : NServiceBusAcceptanceTest
    {
        static Guid HostId = Guid.NewGuid();

        [Test]
        public void Should_enhance_it_with_queue_length_properties()
        {
            var context = Scenario.Define<Context>()
                .WithEndpoint<Subscriber>(b => b.When(ctx => ctx.EndpointsStarted, (session, c) =>
                {
                    session.Subscribe<TestEventMessage1>();
                    session.Subscribe<TestEventMessage2>();
                }))
                .WithEndpoint<Publisher>(c => c.When(ctx => ctx.SubscriptionCount == 2, s =>
                {
                    s.Publish(new TestEventMessage1());
                    s.Publish(new TestEventMessage1());
                    s.Publish(new TestEventMessage2());
                    s.Publish(new TestEventMessage2());
                }))
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

        class Context : ScenarioContext
        {
            public volatile int SubscriptionCount;

            public ConcurrentQueue<IDictionary<string, string>> Headers1 { get; } = new ConcurrentQueue<IDictionary<string, string>>();
            public ConcurrentQueue<IDictionary<string, string>> Headers2 { get; } = new ConcurrentQueue<IDictionary<string, string>>();

            public MetricsContext Data { get; set; }

            public Func<bool> TrackReports;
            public Context()
            {
                TrackReports = () => Headers1.Count == 2 && Headers2.Count == 2;
            }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UniquelyIdentifyRunningInstance().UsingCustomIdentifier(HostId);

                    c.OnEndpointSubscribed<Context>((s, ctx) =>
                    {
                        if (s.SubscriberReturnAddress.Contains("Subscriber"))
                        {
                            Interlocked.Increment(ref ctx.SubscriptionCount);
                        }
                    });

                    var address = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(MonitoringSpy));
                    c.SendMetricDataToServiceControl(address);
                });
            }
        }

        class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                    {
                        c.DisableFeature<AutoSubscribe>();
                    })
                    .AddMapping<TestEventMessage1>(typeof(Publisher))
                    .AddMapping<TestEventMessage2>(typeof(Publisher));
            }

            public class LimitConcurrency : IProvideConfiguration<TransportConfig>
            {
                public TransportConfig GetConfiguration() => new TransportConfig { MaximumConcurrencyLevel = 1 };
            }

            public class TestEventMessage1Handler : IHandleMessages<TestEventMessage1>
            {
                public Context TestContext { get; set; }

                public IBus Bus { get; set; }

                public void Handle(TestEventMessage1 message)
                {
                    TestContext.Headers1.Enqueue(Bus.CurrentMessageContext.Headers);
                }
            }

            public class TestEventMessage2Handler : IHandleMessages<TestEventMessage2>
            {
                public Context TestContext { get; set; }

                public IBus Bus { get; set; }

                public void Handle(TestEventMessage2 message)
                {
                    TestContext.Headers2.Enqueue(Bus.CurrentMessageContext.Headers);
                }
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

            public class LimitConcurrency : IProvideConfiguration<TransportConfig>
            {
                public TransportConfig GetConfiguration() => new TransportConfig { MaximumConcurrencyLevel = 1 };
            }

            class MetricHandler : IHandleMessages<MetricReport>
            {
                public Context TestContext { get; set; }

                public void Handle(MetricReport message)
                {
                    if (TestContext.TrackReports())
                    {
                        TestContext.Data = message.Data;
                    }
                }
            }
        }

        public class TestEventMessage1 : IEvent
        {
        }

        public class TestEventMessage2 : IEvent
        {
        }
    }
}