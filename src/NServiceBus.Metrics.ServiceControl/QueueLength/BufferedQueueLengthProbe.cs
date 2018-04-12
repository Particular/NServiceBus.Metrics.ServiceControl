namespace NServiceBus.Metrics.ServiceControl
{
    using global::ServiceControl.Monitoring.Data;

    class BufferedQueueLengthProbe : IQueueLengthProbe
    {
        public BufferedQueueLengthProbe(RingBuffer buffer, TaggedLongValueWriterV1 writer)
        {
            this.buffer = buffer;
            this.writer = writer;
        }

        public void Signal(string physicalAddress, long length)
        {
            var tag = writer.GetTagId(physicalAddress);
            RingBufferExtensions.WriteTaggedValue(buffer, "QueueLength", length, tag);
        }

        readonly RingBuffer buffer;
        readonly TaggedLongValueWriterV1 writer;
    }
}