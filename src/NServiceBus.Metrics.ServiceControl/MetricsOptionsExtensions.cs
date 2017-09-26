namespace NServiceBus
{
    using System;
    using Metrics.ServiceControl;

    /// <summary>Provides configuration options for Metrics feature</summary>
    public static class MetricsOptionsExtensions
    {
        /// <summary>
        /// Enables sending periodic updates of metric data to ServiceControl
        /// </summary>
        /// <param name="options">The metrics options configuration object.</param>
        /// <param name="serviceControlMetricsAddress">The transport address of the ServiceControl instance</param>
        /// <param name="interval">Interval between consecutive reports</param>
        /// <param name="instanceId">Unique, human-readable, stable between restarts, identifier for running endpoint instance.</param>
        public static void SendMetricDataToServiceControl(this MetricsOptions options, string serviceControlMetricsAddress, TimeSpan interval, string instanceId = null)
        {
            Guard.AgainstNullAndEmpty(nameof(serviceControlMetricsAddress), serviceControlMetricsAddress);
            Guard.AgainstNegativeAndZero(nameof(interval), interval);

            var reporting = ReportingOptions.Get(options);

            reporting.ServiceControlMetricsAddress = serviceControlMetricsAddress;
            reporting.ServiceControlReportingInterval = interval;
            reporting.EndpointInstanceIdOverride = instanceId;
        }
    }
}