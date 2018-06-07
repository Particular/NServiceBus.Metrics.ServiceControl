namespace NServiceBus.Metrics
{
    public class EndpointMetadataReport : IMessage
    {
        public string LocalAddress { get; set; }
        public int Version { get; set; }
    }
}