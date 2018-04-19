using System.Collections.Generic;
using NServiceBus.Metrics.ServiceControl;

class NativeQueueLengthReporter : IReportNativeQueueLengths
{
    readonly QueueLengthReport report;

    public NativeQueueLengthReporter(QueueLengthReport report)
    {
        this.report = report;
    }

    public void ReportQueueLength(string physicalQueueName, long? queueLength)
    {
        report.Queues[physicalQueueName] = queueLength;
    }

    public IEnumerable<string> PhysicalQueues => report.Queues.Keys;
}