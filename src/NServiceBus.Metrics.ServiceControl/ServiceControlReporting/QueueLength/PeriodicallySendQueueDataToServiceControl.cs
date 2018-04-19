using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Metrics.ServiceControl;
using NServiceBus.Metrics.ServiceControl.ServiceControlReporting;
using NServiceBus.Transports;

class PeriodicallySendQueueDataToServiceControl : FeatureStartupTask
{
    public PeriodicallySendQueueDataToServiceControl(QueueLengthReport report, ISendMessages dispatcher, ReportingOptions options)
    {
        this.report = report;
        this.dispatcher = dispatcher;
        this.options = options;
    }

    protected override void OnStart()
    {
        HeaderValues.Add(Headers.EnclosedMessageTypes, "NServiceBus.Metrics.QueueReport");
        HeaderValues.Add(Headers.ContentType, ContentTypes.Json);

        var serviceControlReport = new SendQueueLengthReportToServiceControl(dispatcher, options, HeaderValues, report);

        task = Task.Run(async () =>
        {
            while (cancellationTokenSource.IsCancellationRequested == false)
            {
                serviceControlReport.SendNow();
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
        });
    }

    protected override void OnStop()
    {
        cancellationTokenSource.Cancel();
        task.GetAwaiter().GetResult();
    }

    readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    readonly QueueLengthReport report;
    readonly ISendMessages dispatcher;
    readonly ReportingOptions options;
    public Dictionary<string, string> HeaderValues { get; set; }
    Task task;
}