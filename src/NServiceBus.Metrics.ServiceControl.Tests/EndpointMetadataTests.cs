namespace NServiceBus.Metrics.ServiceControl.Tests;

using System.Text.Json;
using ServiceControlReporting;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
class EndpointMetadataTests
{
    [Test]
    public void Verify_serialization()
    {
        var endpointMetadata1 = new EndpointMetadata("localAddress");
        var endpointMetadata2 = new EndpointMetadata("localAddress");

        byte[] endpointMetadata1Bytes = JsonSerializer.SerializeToUtf8Bytes(endpointMetadata1, ReportingSerializationContext.Default.EndpointMetadata);
        byte[] endpointMetadata2Bytes = JsonSerializer.SerializeToUtf8Bytes(endpointMetadata2, ReportingSerializationContext.Default.EndpointMetadata);

        Assert.That(endpointMetadata1Bytes, Is.EquivalentTo(endpointMetadata2Bytes), "Serializing the same EndpointMetadata should produce the same byte array");
        Approver.Verify(JsonSerializer.Serialize(endpointMetadata2, ReportingSerializationContext.Default.EndpointMetadata));
    }
}