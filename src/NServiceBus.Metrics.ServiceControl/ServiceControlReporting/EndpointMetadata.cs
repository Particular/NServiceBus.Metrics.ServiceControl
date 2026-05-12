namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    sealed class EndpointMetadata(string localAddress)
    {
#pragma warning disable CA1822
        public int PluginVersion => 3;
#pragma warning restore CA1822
        public string LocalAddress { get; private init; } = localAddress;
    }
}