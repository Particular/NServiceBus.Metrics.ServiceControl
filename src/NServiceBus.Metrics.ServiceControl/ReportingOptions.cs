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

        public static ReportingOptions Get(MetricsOptions options) => reporting.GetOrAdd(options, metricsOptions=>
        {
            var reportingOptions = new ReportingOptions();
            metricsOptions.RegisterObservers(probeContext =>
            {
                foreach (var durationProbe in probeContext.Durations)
                {
                    if (durationProbe.Name == "Processing Time")
                    {
                        durationProbe.Register(reportingOptions.OnProcessingTime);
                    }
                    else if (durationProbe.Name == "Critical Time")
                    {
                        durationProbe.Register(reportingOptions.OnCriticalTime);
                    }
                }

                foreach (var signalProbe in probeContext.Signals)
                {
                    if (signalProbe.Name == "Retries")
                    {
                        signalProbe.Register(reportingOptions.OnRetry);
                    }
                }
            });
            return reportingOptions;
        });

        void OnRetry(ref SignalEvent e)
        {
            RetryHandler?.Invoke(ref e);
        }

        public OnEvent<SignalEvent> RetryHandler;

        void OnCriticalTime(ref DurationEvent e)
        {
            CriticalTimeHandler?.Invoke(ref e);
        }

        public OnEvent<DurationEvent> CriticalTimeHandler;

        void OnProcessingTime(ref DurationEvent e)
        {
            ProcessingTimeHandler?.Invoke(ref e);
        }

        public OnEvent<DurationEvent> ProcessingTimeHandler;
    }
}