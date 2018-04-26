namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    class EndpointMetadata
    {
        readonly string localAddress;

        public EndpointMetadata(string localAddress)
        {
            this.localAddress = localAddress;
        }

        public string ToJson()
        {
            return SimpleJson.SerializeObject(new
            {
                Version = 3,
                LocalAddress = localAddress
            });
        }
    }
}