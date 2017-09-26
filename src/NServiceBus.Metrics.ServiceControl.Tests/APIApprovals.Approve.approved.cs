namespace NServiceBus
{
    public class static MetricsOptionsExtensions
    {
        public static void SendMetricDataToServiceControl(this NServiceBus.MetricsOptions options, string serviceControlMetricsAddress, System.TimeSpan interval, string instanceId = null) { }
    }
}