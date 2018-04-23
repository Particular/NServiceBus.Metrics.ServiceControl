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
                Version = 1,
                LocalAddress = localAddress
            });
        }
    }
}