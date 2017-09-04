[assembly: System.Runtime.Versioning.TargetFrameworkAttribute(".NETFramework,Version=v4.5.2", FrameworkDisplayName=".NET Framework 4.5.2")]

namespace NServiceBus.Metrics.ServiceControl
{
    
    public class static ReportingConfigurationExtensions
    {
        public static NServiceBus.Metrics.ServiceControl.ReportingOptions EnableReporting(this NServiceBus.Settings.SettingsHolder settings) { }
        public static NServiceBus.Metrics.ServiceControl.ReportingOptions EnableReporting(this NServiceBus.BusConfiguration endpointConfiguration) { }
    }
    public class ReportingOptions
    {
        public void SendMetricDataToServiceControl(string serviceControlMetricsAddress, string instanceId = null) { }
    }
}