using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Metrics.ServiceControl.AcceptanceTests
{
    using System;
    using Conventions = AcceptanceTesting.Customization.Conventions;
    using EndpointTemplates;

    public class When_reporting_to_ServiceControl_enabled : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_periodically_send_metadata_report()
        {
            var context = Scenario.Define<Context>()
                .WithEndpoint<MonitoredEndpoint>()
                .WithEndpoint<MonitoringEndpoint>()
                .Done(c => c.MetadataReportReceived)
                .Run();

            var localAddress = $"{Conventions.EndpointNamingConvention(typeof(MonitoredEndpoint))}@{Environment.MachineName}";
            
            Assert.AreEqual(1, context.Report.PluginVersion);
            Assert.AreEqual(localAddress, context.Report.LocalAddress);
        }

        class Context : ScenarioContext
        {
            public bool MetadataReportReceived { get; set; }
            public EndpointMetadataReport Report { get; set; }
            public string LocalAddress { get; set; }
        }

        class MonitoredEndpoint : EndpointConfigurationBuilder
        {
            public MonitoredEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                    {
                        var monitoringSpyAddress = Conventions.EndpointNamingConvention(typeof(MonitoringEndpoint));

                        c.SendMetricDataToServiceControl(monitoringSpyAddress);
                    });
            }
        }

        class MonitoringEndpoint : EndpointConfigurationBuilder
        {
            public MonitoringEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseSerialization<JsonSerializer>();
                }).IncludeType<EndpointMetadataReport>();
            }

            public class MetadataHandler : IHandleMessages<EndpointMetadataReport>
            {
                readonly Context context;

                public MetadataHandler(Context context)
                {
                    this.context = context;
                }

                public void Handle(EndpointMetadataReport message)
                {
                    context.MetadataReportReceived = true;
                    context.Report = message;
                }
            }
        }
    }

    }