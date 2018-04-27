namespace NServiceBus.Metrics.ServiceControl
{
    public class static BusConfigurationExtensions
    {
        public static void SendMetricDataToServiceControl(this NServiceBus.BusConfiguration busConfiguration, string serviceControlMetricsAddress, string instanceId = null) { }
        public static void SetServiceControlMetricsMessageTTBR(this NServiceBus.BusConfiguration busConfiguration, System.TimeSpan timeToBeReceived) { }
    }
    public interface IReportNativeQueueLength
    {
        System.Collections.Generic.IEnumerable<string> MonitoredQueues { get; }
        void ReportQueueLength(string physicalQueueName, long queueLength);
    }
}