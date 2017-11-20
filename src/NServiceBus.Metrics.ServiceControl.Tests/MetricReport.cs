namespace NServiceBus.Metrics
{
    public class MetricReport : IMessage
    {
        public MetricsContext Data { get; set; }
    }

    public class MetricsContext
    {
        public Counter[] Counters { get; set; }
        public Gauge[] Gauges { get; set; }
        public string Context { get; set; }
    }

    public class Gauge
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public string[] Tags { get; set; }
    }

    public class Counter
    {
        public string Name { get; set; }
        public long Count { get; set; }
        public string[] Tags { get; set; }
    }
}