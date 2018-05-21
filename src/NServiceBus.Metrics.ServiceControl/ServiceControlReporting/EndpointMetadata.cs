namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    class EndpointMetadata
    {
        string localAddress;

        public EndpointMetadata(string localAddress)
        {
            this.localAddress = localAddress;
        }

        public string ToJson()
        {
            return SimpleJson.SerializeObject(new
            {
                PluginVersion = 1, 
                LocalAddress = localAddress
            });
        }
    }
}