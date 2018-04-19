namespace NServiceBus.Metrics.ServiceControl
{
    using System.Collections.Generic;

    /// <summary>
    /// Reports native queue lengths to a monitoring server
    /// NOTE: Does not need to immediately report. Might cache values and report them later
    /// </summary>
    public interface IReportNativeQueueLengths
    {
        /// <summary>
        /// The list of physcial queue instances that are being reported on
        /// </summary>
        IEnumerable<string> PhysicalQueues { get; }

        /// <summary>
        /// Report the length of a single physical queue
        /// NOTE: If reported as null then only the linkage between endpoint instance and physical queue is reported
        /// </summary>
        void ReportQueueLength(string physicalQueue, long? queueLength);
    }
}