namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Performance.TimeToBeReceived;
    using Routing;
    using Transport;

    class MetadataReport(IMessageDispatcher dispatcher, ReportingOptions options, Dictionary<string, string> headers, EndpointMetadata endpointMetadata)
    {
        public async Task RunReportAsync(CancellationToken cancellationToken = default)
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, endpointMetadataBytes);
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

        readonly UnicastAddressTag destination = new(options.ServiceControlMetricsAddress);
        readonly TimeSpan timeToBeReceived = options.TimeToBeReceived;
        readonly byte[] endpointMetadataBytes = JsonSerializer.SerializeToUtf8Bytes(endpointMetadata, ReportingSerializationContext.Default.EndpointMetadata);

        static readonly ILog Log = LogManager.GetLogger<MetadataReport>();
    }
}