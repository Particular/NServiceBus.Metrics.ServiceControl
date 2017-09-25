namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Collections.Concurrent;

    class ReportingOptions
    {
        static readonly ConcurrentDictionary<MetricsOptions, ReportingOptions> reporting = new ConcurrentDictionary<MetricsOptions, ReportingOptions>();
        internal string ServiceControlMetricsAddress;
        internal TimeSpan ServiceControlReportingInterval;
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

        public static ReportingOptions Get(MetricsOptions options) => reporting.GetOrAdd(options, metricsOptions => new ReportingOptions());
    }
}