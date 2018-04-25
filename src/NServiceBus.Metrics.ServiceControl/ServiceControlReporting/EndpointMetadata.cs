namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    class EndpointMetadata
    {
        public EndpointMetadata(string localAddress)
        {
            this.localAddress = localAddress;
        }

        public string ToJson()
        {
            return SimpleJson.SerializeObject(new
            {
                Version = 2,
                LocalAddress = localAddress
            });
        }

        readonly string localAddress;
    }
}