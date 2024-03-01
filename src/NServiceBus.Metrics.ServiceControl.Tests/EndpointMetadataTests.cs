namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using NServiceBus.Metrics.ServiceControl.ServiceControlReporting;
    using NUnit.Framework;
    using Particular.Approvals;

    [TestFixture]
    class EndpointMetadataTests
    {
        [Test]
        public void Verify_serialization()
        {
            var endpointMetadata = new EndpointMetadata("localAddress");
            var json = endpointMetadata.ToJson();

            Approver.Verify(json);
        }
    }
}
