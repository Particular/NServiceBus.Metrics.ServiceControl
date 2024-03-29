﻿namespace NServiceBus.Metrics.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_reporting_to_ServiceControl : NServiceBusAcceptanceTest
    {
        static string MonitoringSpyAddress => Conventions.EndpointNamingConvention(typeof(MonitoringSpy));
        static Guid HostId = Guid.NewGuid();

        [Test]
        public async Task Should_send_metadata_to_configured_queue()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>()
                .WithEndpoint<MonitoringSpy>()
                .Done(c => c.Report != null)
                .Run()
                .ConfigureAwait(false);

            Assert.IsNotNull(context.Report);
            Assert.AreEqual(3, context.Report.PluginVersion);
            Assert.IsNotEmpty(context.Report.LocalAddress);

            Assert.AreEqual(HostId.ToString("N"), context.Headers[Headers.OriginatingHostId]);
            Assert.AreEqual("NServiceBus.Metrics.EndpointMetadataReport", context.Headers[Headers.EnclosedMessageTypes]);
            Assert.AreEqual(ContentTypes.Json, context.Headers[Headers.ContentType]);
        }

        class Context : ScenarioContext
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

        class MonitoringSpy : EndpointConfigurationBuilder
        {
            public MonitoringSpy()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseSerialization<SystemJsonSerializer>();
                    c.LimitMessageProcessingConcurrencyTo(1);
                }).IncludeType<EndpointMetadataReport>();
            }

            public class MetricHandler : IHandleMessages<EndpointMetadataReport>
            {
                Context testContext;

                public MetricHandler(Context context)
                {
                    testContext = context;
                }

                public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
                {
                    testContext.Report = message;
                    testContext.Headers = context.MessageHeaders;

                    return Task.CompletedTask;
                }
            }
        }
    }
}