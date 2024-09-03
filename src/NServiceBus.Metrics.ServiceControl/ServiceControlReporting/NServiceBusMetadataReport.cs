namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Performance.TimeToBeReceived;
    using Routing;
    using Transport;

    class NServiceBusMetadataReport
    {
        public NServiceBusMetadataReport(IMessageDispatcher dispatcher, ReportingOptions options, Dictionary<string, string> headers, EndpointMetadata endpointMetadata)
        {
            this.dispatcher = dispatcher;
            this.headers = headers;
            this.endpointMetadata = endpointMetadata;

            destination = new UnicastAddressTag(options.ServiceControlMetricsAddress);
            timeToBeReceived = options.TimeToBeReceived;
        }

        public async Task RunReportAsync(CancellationToken cancellationToken = default)
        {
            var stringBody = endpointMetadata.ToJson();
            var body = Encoding.UTF8.GetBytes(stringBody);

            var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, body);
            var dispatchProperties = new DispatchProperties
            {
                DiscardIfNotReceivedBefore = new DiscardIfNotReceivedBefore(timeToBeReceived)
            };

            var operation = new TransportOperation(message, destination, dispatchProperties, DispatchConsistency.Default);

            try
            {
                await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Log.Error($"Error while sending metric data to {destination.Destination}.", ex);
            }
        }

        readonly UnicastAddressTag destination;
        readonly IMessageDispatcher dispatcher;
        readonly Dictionary<string, string> headers;
        readonly EndpointMetadata endpointMetadata;
        readonly TimeSpan timeToBeReceived;

        static readonly ILog Log = LogManager.GetLogger<NServiceBusMetadataReport>();
    }
}