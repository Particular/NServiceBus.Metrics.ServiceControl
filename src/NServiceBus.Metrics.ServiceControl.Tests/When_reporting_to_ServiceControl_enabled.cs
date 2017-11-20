namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;
    using Pipeline.Contexts;

    public class When_reporting_to_ServiceControl_enabled : NServiceBusAcceptanceTest
    {
        static readonly byte[] MyMessageNameBytes = new UTF8Encoding(false).GetBytes(typeof(MyMessage).AssemblyQualifiedName);
        static readonly byte[] ThrowingMessageNameBytes = new UTF8Encoding(false).GetBytes(typeof(ThrowingMessage).AssemblyQualifiedName);

        const string CustomInstanceId = "my-custom-instance";
        const string Message = "Thrown on purpose!";

        [Test]
        public void Should_report_properly()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                        bus.SendLocal(new MyMessage());
                    }

                    bus.SendLocal(new ThrowingMessage());
                }))
                .WithEndpoint<MonitoringMock>()
                .Done(c => c.Reports.Count == 3)
                .AllowExceptions(ex => true)
                .Run();

            // Processing Time
            {
                var report = context.Reports["ProcessingTime"];
                AssertMetricType(report, "ProcessingTime");
                AssertInstanceId(report);
                AssertContentType(report);
                AssertProperTagging(report, MyMessageNameBytes);
            }

            // Critical Time
            {
                var report = context.Reports["CriticalTime"];
                AssertMetricType(report, "CriticalTime");
                AssertInstanceId(report);
                AssertContentType(report);
                AssertProperTagging(report, MyMessageNameBytes);
            }

            // Retries
            {
                var report = context.Reports["Retries"];
                AssertMetricType(report, "Retries");
                AssertInstanceId(report);
                AssertContentType(report);
                AssertProperTagging(report, ThrowingMessageNameBytes);
            }
        }

        static void AssertMetricType(Report report, string name)
        {
            Assert.AreEqual(name, report.Headers["NServiceBus.Metric.Type"]);
        }

        static void AssertProperTagging(Report report, byte[] nameBytes)
        {
            // dummy assert for containing the name of message in the message body
            Assert.True(ContainsPattern(report.Body, nameBytes), "The message should contain the fully qualified name of the reported message");
        }

        static void AssertContentType(Report report)
        {
            Assert.AreEqual("TaggedLongValueWriterOccurrence", report.Headers["NServiceBus.ContentType"]);
        }

        static void AssertInstanceId(Report report)
        {
            Assert.AreEqual(CustomInstanceId, report.Headers["NServiceBus.Metric.InstanceId"]);
        }

        static bool ContainsPattern(byte[] source, byte[] pattern)
        {
            for (var i = 0; i < source.Length - pattern.Length; i++)
            {
                if (source.Skip(i).Take(pattern.Length).SequenceEqual(pattern))
                {
                    return true;
                }
            }

            return false;
        }

        public class Context : ScenarioContext
        {
            public ConcurrentDictionary<string, Report> Reports = new ConcurrentDictionary<string, Report>();
        }

        public class Report
        {
            public Dictionary<string, string> Headers { get; set; }
            public byte[] Body { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg => cfg.SendMetricDataToServiceControl(Conventions.EndpointNamingConvention(typeof(MonitoringMock)), CustomInstanceId));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public void Handle(MyMessage message)
                {
                    Thread.Sleep(100);
                }
            }

            public class ThrowingMessageHandler : IHandleMessages<ThrowingMessage>
            {
                public void Handle(ThrowingMessage message)
                {
                    throw new Exception(Message);
                }
            }
        }

        public class MonitoringMock : EndpointConfigurationBuilder
        {
            public MonitoringMock()
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
                    var metric = msg.Headers["NServiceBus.Metric.Type"];

                    var report = new Report
                    {
                        Body = msg.Body.ToArray(),
                        Headers = msg.Headers
                    };

                    Context.Reports[metric] = report;
                }
            }
        }

        public class MyMessage : IMessage
        { }

        public class ThrowingMessage : IMessage
        { }
    }
}