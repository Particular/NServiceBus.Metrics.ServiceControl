﻿namespace NServiceBus.Metrics.ServiceControl
{
    using Configuration.AdvanceExtensibility;

    /// <summary>
    /// Extends Endpoint Configuration to provide Metric options
    /// </summary>
    public static class BusConfigurationExtensions
    {
        /// <summary>
        /// Configures reporters with an address of ServiceControl.Monitoring.
        /// </summary>
        /// <param name="busConfiguration">Bus configuration.</param>
        /// <param name="serviceControlMetricsAddress">The address of ServiceControl.Monitoring.</param>
        /// <param name="instanceId">A custom instance id used for reporting.</param>
        public static void SendMetricDataToServiceControl(this BusConfiguration busConfiguration, string serviceControlMetricsAddress, string instanceId = null)
        {
            var settings = busConfiguration.GetSettings();

            if (settings.TryGet<ReportingOptions>(out var options) == false)
            {
                options = new ReportingOptions();
                settings.Set<ReportingOptions>(options);
            }

            options.ServiceControlMetricsAddress = serviceControlMetricsAddress;
            options.EndpointInstanceIdOverride = instanceId;
        }
    }
}