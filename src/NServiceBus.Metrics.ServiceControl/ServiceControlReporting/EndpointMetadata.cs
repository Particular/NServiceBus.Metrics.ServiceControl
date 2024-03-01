namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    using System.Text.Json;

    class EndpointMetadata
    {
        readonly string localAddress;

        public EndpointMetadata(string localAddress)
        {
            this.localAddress = localAddress;
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(new
            {
                PluginVersion = 3,
                LocalAddress = localAddress
            });
        }
    }
}