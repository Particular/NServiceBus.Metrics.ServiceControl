[assembly: System.Runtime.Versioning.TargetFrameworkAttribute(".NETFramework,Version=v4.5.2", FrameworkDisplayName=".NET Framework 4.5.2")]

namespace NServiceBus.Metrics.ServiceControl
{
    
    public class static BusConfigurationExtensions
    {
        public static void SendMetricDataToServiceControl(this NServiceBus.BusConfiguration busConfiguration, string serviceControlMetricsAddress, string instanceId = null) { }
        public static void SetServiceControlMetricsMessageTTBR(this NServiceBus.BusConfiguration busConfiguration, System.TimeSpan timeToBeReceived) { }
    }
}