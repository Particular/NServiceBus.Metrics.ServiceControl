namespace NServiceBus.Metrics.ServiceControl
{
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
        Buffers buffers;

        public QueueLengthBufferReporter(Buffers buffers)
        {
            this.buffers = buffers;
        }

        public void ReportQueueLength(string physicalQueueName, long queueLength)
        {
            buffers.ReportQueueLength(queueLength, physicalQueueName);
        }
    }
}
