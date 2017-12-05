namespace NServiceBus
{
    public class static MetricsOptionsExtensions
    {
        public static void SendMetricDataToServiceControl(this NServiceBus.MetricsOptions options, string serviceControlMetricsAddress, System.TimeSpan interval, string instanceId = null) { }
        public static void SetServiceControlTTBR(this NServiceBus.MetricsOptions options, System.TimeSpan timeToBeReceived) { }
    }
}