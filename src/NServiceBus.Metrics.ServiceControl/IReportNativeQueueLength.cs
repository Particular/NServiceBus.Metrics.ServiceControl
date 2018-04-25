namespace NServiceBus.Metrics.ServiceControl
{
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
    }

    class QueueLengthBufferReporter : IReportNativeQueueLength
    {
        RingBuffer buffer;
        TaggedLongValueWriterV1 writer;

        public QueueLengthBufferReporter(RingBuffer buffer, TaggedLongValueWriterV1 writer)
        {
            this.buffer = buffer;
            this.writer = writer;
        }

        public void ReportQueueLength(string physicalQueueName, long queueLength)
        {
            var tag = writer.GetTagId(physicalQueueName);

            RingBufferExtensions.WriteTaggedValue(buffer, "QueueLength", queueLength, tag);
        }
    }
}