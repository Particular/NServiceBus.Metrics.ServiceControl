namespace NServiceBus.Metrics.ServiceControl
{
    class ReportingOptions
    {
        internal string ServiceControlMetricsAddress;
        internal string EndpointInstanceIdOverride;

        internal bool TryGetValidEndpointInstanceIdOverride(out string instanceId)
        {
            if (string.IsNullOrEmpty(EndpointInstanceIdOverride) == false)
            {
                instanceId = EndpointInstanceIdOverride;
                return true;
            }

            instanceId = null;
            return false;
        }
    }
}