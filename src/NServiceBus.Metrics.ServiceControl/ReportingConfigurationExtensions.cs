namespace NServiceBus.Metrics.ServiceControl
{
    using Settings;

    /// <summary>
    /// Extends Endpoint Configuration to provide Metric options
    /// </summary>
    public static class ReportingConfigurationExtensions
    {
        /// <summary>
        /// Enables reporting to ServiceControl.Monitoring.
        /// </summary>
        /// <param name="settings">The settings to enable the reporting feature on.</param>
        /// <returns>An object containing configuration options for the reporting feature.</returns>
        public static ReportingOptions EnableMetrics(this SettingsHolder settings)
        {
            if (settings.TryGet<ReportingOptions>(out var options) == false)
            {
                options = new ReportingOptions();
                settings.Set<ReportingOptions>(options);
            }

            settings.Set(typeof(ServiceControlMonitoring).FullName, true);
            return options;
        }

        /// <summary>
        /// Enables the reporting feature.
        /// </summary>
        /// <param name="endpointConfiguration">The endpoint configuration to enable the reporting feature on.</param>
        /// <returns>An object containing configuration options for the reporting feature.</returns>
        public static ReportingOptions EnableMetrics(this Configure endpointConfiguration)
        {
            return EnableMetrics(endpointConfiguration.Settings);
        }
    }
}