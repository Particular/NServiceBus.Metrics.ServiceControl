namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting;

using System.Text.Json.Serialization;

[JsonSerializable(typeof(EndpointMetadata))]
partial class ReportingSerializationContext : JsonSerializerContext;