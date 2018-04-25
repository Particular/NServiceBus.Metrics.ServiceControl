using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    using DeliveryConstraints;
    using Performance.TimeToBeReceived;

    class EndpointMetadataReport
    {
        public EndpointMetadataReport(IDispatchMessages dispatcher, ReportingOptions options, Dictionary<string, string> headers, EndpointMetadata endpointMetadata)
        {
            this.dispatcher = dispatcher;
            this.headers = headers;
            this.endpointMetadata = endpointMetadata;

            destination = new UnicastAddressTag(options.ServiceControlMetricsAddress);
            timeToBeReceived = options.TimeToBeReceived;
        }

        public async Task RunReportAsync()
        {
            var stringBody = endpointMetadata.ToJson();
            var body = Encoding.UTF8.GetBytes(stringBody);

            var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, body);
            var constraints = new List<DeliveryConstraint>
            {
                new DiscardIfNotReceivedBefore(timeToBeReceived)
            };

            var operation = new TransportOperation(message, destination, DispatchConsistency.Default, constraints);

            try
            {
                await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                log.Error($"Error while sending metric data to {destination}.", exception);
            }
        }

        readonly UnicastAddressTag destination;
        readonly IDispatchMessages dispatcher;
        readonly Dictionary<string, string> headers;
        readonly EndpointMetadata endpointMetadata;
        readonly TimeSpan timeToBeReceived;

        static ILog log = LogManager.GetLogger<EndpointMetadataReport>();
    }
}