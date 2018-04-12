namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DeliveryConstraints;
    using Extensibility;
    using global::ServiceControl.Monitoring.Data;
    using Logging;
    using ObjectBuilder;
    using Performance.TimeToBeReceived;
    using Routing;
    using Transport;

    class RawDataReporterFactory
    {
        const string TaggedValueMetricContentType = "TaggedLongValueWriterOccurrence";

        public RawDataReporterFactory(IBuilder builder, ReportingOptions options, Dictionary<string, string> headers)
        {
            this.builder = builder;
            this.options = options;
            this.headers = headers;
        }

        internal RawDataReporter CreateReporter(string metricType, RingBuffer buffer, TaggedLongValueWriterV1 writer)
        {
            var reporterHeaders = new Dictionary<string, string>(headers)
            {
                {Headers.ContentType, TaggedValueMetricContentType},
                {MetricHeaders.MetricType, metricType}
            };

            var dispatcher = builder.Build<IDispatchMessages>();
            var address = new UnicastAddressTag(options.ServiceControlMetricsAddress);

            async Task Sender(byte[] body)
            {
                var message = new OutgoingMessage(Guid.NewGuid().ToString(), reporterHeaders, body);
                var constraints = new List<DeliveryConstraint>
                {
                    new DiscardIfNotReceivedBefore(options.TimeToBeReceived)
                };
                var operation = new TransportOperation(message, address, DispatchConsistency.Default, constraints);
                try
                {
                    await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag())
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log.Error($"Error while reporting raw data to {options.ServiceControlMetricsAddress}.", ex);
                }
            }

            return new RawDataReporter(Sender, buffer, (entries, binaryWriter) => writer.Write(binaryWriter, entries), 2048, options.ServiceControlReportingInterval);
        }

        readonly IBuilder builder;
        readonly ReportingOptions options;
        readonly Dictionary<string, string> headers;

        static readonly ILog log = LogManager.GetLogger<RawDataReporterFactory>();
    }
}