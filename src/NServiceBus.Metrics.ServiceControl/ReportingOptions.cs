namespace NServiceBus.Metrics.ServiceControl
{
    using System;

    class ReportingOptions
    {
        internal string ServiceControlMetricsAddress;
        internal string EndpointInstanceIdOverride;
        public TimeSpan TimeToBeReceived => TimeSpan.FromDays(7);
        
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