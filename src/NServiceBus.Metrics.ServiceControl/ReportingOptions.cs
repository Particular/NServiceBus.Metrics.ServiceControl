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
        public TimeSpan TimeToBeReceived { get; set; } = TimeSpan.FromDays(7);
        Action createMetricReporters;
        bool createReportersCalled;

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

        internal void CreateReporters()
        {
            if (createReportersCalled)
            {
                throw new Exception("CreateReporters has already been called, and can only be called once.");
            }

            createReportersCalled = true;
            if (createMetricReporters != null)
            {
                createMetricReporters();
                createMetricReporters = null;
            }
        }

        internal void OnCreateReporters(Action createMetricReporters)
        {
            if (createReportersCalled)
            {
                createMetricReporters();
            }
            else
            {
                this.createMetricReporters = createMetricReporters;
            }
        }
    }
}