namespace NServiceBus.Metrics.ServiceControl
{
    using System.Collections.Generic;
    using global::ServiceControl.Monitoring.Data;

    /// <summary>
    /// Reports native queue length to ServiceControl Monitoring
    /// </summary>
    public interface IReportNativeQueueLength
    {
        /// <summary>
        /// Reports the length of a queue to be sent to ServiceControl Monitoring
        /// </summary>
        void ReportQueueLength(string physicalQueueName, long queueLength);

        /// <summary>
        /// A list of queues that are monitored
        /// </summary>
        IEnumerable<string> MonitoredQueues { get; }
    }

    class QueueLengthBufferReporter : IReportNativeQueueLength
    {
        RingBuffer buffer;
        TaggedLongValueWriterV1 writer;
        string[] monitoredQueues;

        public QueueLengthBufferReporter(RingBuffer buffer, TaggedLongValueWriterV1 writer, params string[] monitoredQueues)
        {
            this.buffer = buffer;
            this.writer = writer;
            this.monitoredQueues = monitoredQueues;
        }

        public void ReportQueueLength(string physicalQueueName, long queueLength)
        {
            var tag = writer.GetTagId(physicalQueueName);

            RingBufferExtensions.WriteTaggedValue(buffer, "QueueLength", queueLength, tag);
        }

        public IEnumerable<string> MonitoredQueues => monitoredQueues;
    }
}
