using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using Conventions = AcceptanceTesting.Customization.Conventions;
    using EndpointTemplates;

    public class When_reporting_to_ServiceControl_enabled : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_periodically_send_metadata_report()
        {
            Scenario.Define<Context>()
                .WithEndpoint<MonitoredEndpoint>()
                .WithEndpoint<MonitoringEndpoint>()
                .Done(c => c.MetadataReportReceived)
                .Run();
        }

        class Context : ScenarioContext
        {
            public bool MetadataReportReceived { get; set; }
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
                }
            }
        }
    }
}