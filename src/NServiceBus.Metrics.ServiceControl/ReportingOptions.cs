namespace NServiceBus.Metrics.ServiceControl
{
    /// <summary>
    /// Provides configuration options for reporting to ServiceControl.Monitoring.
    /// </summary>
    public class ReportingOptions
    {
        /// <summary>
        /// Configures reporters with an address of ServiceControl.Monitoring.
        /// </summary>
        /// <param name="serviceControlMetricsAddress">The address of ServiceControl.Monitoring.</param>
        /// <param name="instanceId">A custom instance id used for reporting.</param>
        public void SendMetricDataToServiceControl(string serviceControlMetricsAddress, string instanceId = null)
        {
            ServiceControlMetricsAddress = serviceControlMetricsAddress;
            endpointInstanceIdOverride = instanceId;
        }

        internal string ServiceControlMetricsAddress;
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