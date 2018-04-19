using System;
using System.Collections.Generic;
using System.Text;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Metrics.ServiceControl;
using NServiceBus.Metrics.ServiceControl.ServiceControlReporting;
using NServiceBus.Transports;
using NServiceBus.Unicast;

class SendQueueLengthReportToServiceControl
{
    QueueLengthReport report;
    ISendMessages dispatcher;
    Dictionary<string, string> headers;
    string destination;
    TimeSpan ttbr;

    public SendQueueLengthReportToServiceControl(ISendMessages dispatcher, ReportingOptions options, Dictionary<string, string> headers, QueueLengthReport report)
    {
        this.dispatcher = dispatcher;
        this.headers = headers;
        destination = options.ServiceControlMetricsAddress;
        ttbr = options.TimeToBeReceived;
        this.report = report;
    }

    public void SendNow()
    {
        report.TimeStamp = DateTime.UtcNow.Ticks;
        var stringBody = SimpleJson.SerializeObject(report);
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

    static ILog log = LogManager.GetLogger<SendQueueLengthReportToServiceControl>();
}
