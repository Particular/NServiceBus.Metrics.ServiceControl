namespace NServiceBus.Metrics.ServiceControl
{
    using System.Collections.Generic;

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

    sealed class QueueLengthBufferReporter(
        RingBuffer buffer,
        TaggedLongValueWriterV1 writer,
        params string[] monitoredQueues)
        : IReportNativeQueueLength
    {
        public void ReportQueueLength(string physicalQueueName, long queueLength)
        {
            var tag = writer.GetTagId(physicalQueueName);

            buffer.WriteTaggedValue("QueueLength", queueLength, tag);
        }

        public IEnumerable<string> MonitoredQueues => monitoredQueues;
    }
}
