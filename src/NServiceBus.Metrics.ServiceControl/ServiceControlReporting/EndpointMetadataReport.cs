namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Logging;
    using Transports;
    using Unicast;

    class EndpointMetadataReport
    {
        public EndpointMetadataReport(ISendMessages dispatcher, ReportingOptions options, Dictionary<string, string> headers, EndpointMetadata endpointMetadata)
        {
            this.dispatcher = dispatcher;
            this.headers = headers;
            this.endpointMetadata = endpointMetadata;
            destination = options.ServiceControlMetricsAddress;
            ttbr = options.TimeToBeReceived;
        }

        public void RunReport()
        {
            var stringBody = endpointMetadata.ToJson();
            var body = Encoding.UTF8.GetBytes(stringBody);

            try
            {
                dispatcher.Send(new TransportMessage(Guid.NewGuid().ToString(), headers)
                {
                    Body = body,
                    MessageIntent = MessageIntentEnum.Send,

                    // TTBR is copied to the TransportMessage by the infrastructure before it hits. If ISendMessages is called manually, it needs to be passed in here
                    TimeToBeReceived = ttbr,
                }, new SendOptions(destination)
                {
                    EnlistInReceiveTransaction = false,
                });
            }
            catch (Exception exception)
            {
                log.Error($"Error while sending metric data to {destination}.", exception);
            }
        }

        readonly EndpointMetadata endpointMetadata;
        readonly string destination;
        readonly ISendMessages dispatcher;
        readonly Dictionary<string, string> headers;
        readonly TimeSpan ttbr;
        static ILog log = LogManager.GetLogger<EndpointMetadataReport>();
    }
}