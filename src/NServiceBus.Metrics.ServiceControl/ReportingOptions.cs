namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Runtime.CompilerServices;

    class ReportingOptions
    {
        static readonly ConditionalWeakTable<MetricsOptions, ReportingOptions> reporting = new ConditionalWeakTable<MetricsOptions, ReportingOptions>();

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

        public static ReportingOptions Get(MetricsOptions options) => reporting.GetOrCreateValue(options);
    }
}