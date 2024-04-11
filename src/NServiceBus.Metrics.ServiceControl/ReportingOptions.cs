namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Collections.Concurrent;

    class ReportingOptions
    {
#pragma warning disable PS0025 // Dictionary keys should implement IEquatable<T> - Valid use of lookup-by-reference
        static readonly ConcurrentDictionary<MetricsOptions, ReportingOptions> reporting = new ConcurrentDictionary<MetricsOptions, ReportingOptions>();
#pragma warning restore PS0025
        internal string ServiceControlMetricsAddress;
        internal TimeSpan ServiceControlReportingInterval;
        internal string EndpointInstanceIdOverride;
        public TimeSpan TimeToBeReceived { get; set; } = TimeSpan.FromDays(7);

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