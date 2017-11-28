namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Logging;
    using Transports;
    using Unicast;

    class NServiceBusMetricReport
    {
        public NServiceBusMetricReport(ISendMessages dispatcher, ReportingOptions options, Dictionary<string, string> headers, MetricsContext metricsContext)
        {
            this.dispatcher = dispatcher;
            this.headers = headers;
            this.metricsContext = metricsContext;
            destination = options.ServiceControlMetricsAddress;
        }

        public void RunReport()
        {
            var stringBody = $@"{{""Data"" : {metricsContext.ToJson()}}}";
            var body = Encoding.UTF8.GetBytes(stringBody);

            try
            {
                dispatcher.Send(new TransportMessage(Guid.NewGuid().ToString(), headers)
                {
                    Body = body,
                    MessageIntent = MessageIntentEnum.Send
                }, new SendOptions(destination));
            }
            catch (Exception exception)
            {
                log.Error($"Error while sending metric data to {destination}.", exception);
            }
        }

        readonly MetricsContext metricsContext;
        readonly string destination;
        readonly ISendMessages dispatcher;
        readonly Dictionary<string, string> headers;
        static ILog log = LogManager.GetLogger<NServiceBusMetricReport>();
    }
}