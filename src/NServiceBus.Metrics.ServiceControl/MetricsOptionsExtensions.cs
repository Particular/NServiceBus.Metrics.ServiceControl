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
        /// <param name="interval">Maximum interval between consecutive reports. Recommended to use a value between 10 and 60 seconds. Metrics messages will be dispatched more frequently when the instance is under load.</param>
        /// <param name="instanceId">Only required if this instanceId needs to be different from the instance id added to all outgoing messages by NServiceBus core. Unique, human-readable, stable between restarts, identifier for running endpoint instance.</param>
        public static void SendMetricDataToServiceControl(this MetricsOptions options, string serviceControlMetricsAddress, TimeSpan interval, string instanceId = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceControlMetricsAddress);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

            var reporting = ReportingOptions.Get(options);

            reporting.ServiceControlMetricsAddress = serviceControlMetricsAddress;
            reporting.ServiceControlReportingInterval = interval;
            reporting.EndpointInstanceIdOverride = instanceId;
        }

        /// <summary>
        /// Configures messages send to Service Control to be expired after <paramref name="timeToBeReceived"/>.
        /// </summary>
        /// <param name="options">The metrics options configuration object.</param>
        /// <param name="timeToBeReceived">Time to be received.</param>
        public static void SetServiceControlMetricsMessageTTBR(this MetricsOptions options, TimeSpan timeToBeReceived)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToBeReceived, TimeSpan.Zero);

            var reporting = ReportingOptions.Get(options);

            reporting.TimeToBeReceived = timeToBeReceived;
        }
    }
}
