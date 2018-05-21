namespace NServiceBus.Metrics.ServiceControl
{
    using System.Collections.Generic;

    /// <summary>
    /// Reports native queue length to ServiceControl Monitoring
    /// </summary>
    public interface IReportNativeQueueLength
    {
        /// <summary>
        /// A list of queues that are monitored
        /// </summary>
        IEnumerable<string> MonitoredQueues { get; }

        /// <summary>
        /// Reports the length of a queue to be sent to ServiceControl Monitoring
        /// </summary>
        void ReportQueueLength(string physicalQueueName, long queueLength);
    }

    class QueueLengthBufferReporter : IReportNativeQueueLength
    {
        Buffers buffers;
        string[] monitoredQueues;

        public QueueLengthBufferReporter(Buffers buffers, params string[] monitoredQueues)
        {
            this.buffers = buffers;
            this.monitoredQueues = monitoredQueues;
        }

        public void ReportQueueLength(string physicalQueueName, long queueLength)
        {
            buffers.ReportQueueLength(queueLength, physicalQueueName);
        }

        public IEnumerable<string> MonitoredQueues => monitoredQueues;
    }
}
