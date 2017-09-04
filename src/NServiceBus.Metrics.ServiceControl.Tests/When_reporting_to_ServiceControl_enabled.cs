namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
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
        const string CustomInstanceId = "my-custom-instance";

        [Test]
        public void Should_report_properly_formatted_message()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                        bus.SendLocal(new MyMessage());
                    }
                }))
                .WithEndpoint<MonitoringMock>()
                .Done(c => c.Body != null)
                .Run();

            Assert.AreEqual("ProcessingTime", context.Headers["NServiceBus.Metric.Type"]);
            Assert.AreEqual(CustomInstanceId, context.Headers["NServiceBus.Metric.InstanceId"]);
            Assert.AreEqual("TaggedLongValueWriterOccurrence", context.Headers["NServiceBus.ContentType"]);

            // dummy assert for containing the name of message in the message body
            var fullyQualifiedName = new UTF8Encoding(false).GetBytes(typeof(MyMessage).AssemblyQualifiedName);
            Assert.True(ContainsPattern(context.Body, fullyQualifiedName), "The message should contain the fully qualified name of the reported message");
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
                    Context.Headers = context.PhysicalMessage.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    Context.Body = context.PhysicalMessage.Body.ToArray();
                }
            }
        }

        public class MyMessage : IMessage
        { }
    }
}