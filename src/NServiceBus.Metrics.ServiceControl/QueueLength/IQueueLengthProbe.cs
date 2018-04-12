namespace NServiceBus.Metrics.ServiceControl
{
    /// <summary>
    /// A metrics probe for queue length
    /// </summary>
    public interface IQueueLengthProbe
    {
        /// <summary>
        /// Updates metrics with new queue length information
        /// </summary>
        /// <param name="physicalAddress">The physical address of the queue</param>
        /// <param name="length">The current length of the queue</param>
        void Signal(string physicalAddress, long length);
    }
}