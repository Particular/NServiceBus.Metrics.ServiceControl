namespace NServiceBus.Metrics.ServiceControl
{
    using System;

    class MetricsOptions
    {
        public void SendMetricDataToServiceControl(string serviceControlMetricsAddress, TimeSpan interval, string instanceId = null)
        {
            ServiceControlMetricsAddress = serviceControlMetricsAddress;
            ServiceControlReportingInterval = interval;
            endpointInstanceIdOverride = instanceId;
        }

        internal string ServiceControlMetricsAddress;
        internal TimeSpan ServiceControlReportingInterval;
        string endpointInstanceIdOverride;

        internal bool TryGetValidEndpointInstanceIdOverride(out string instanceId)
        {
            if (string.IsNullOrEmpty(endpointInstanceIdOverride) == false)
            {
                instanceId = endpointInstanceIdOverride;
                return true;
            }

            instanceId = null;
            return false;
        }
    }
}