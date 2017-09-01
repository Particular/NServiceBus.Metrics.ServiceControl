namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Threading;
    using global::ServiceControl.Monitoring.Data;

    class Buffers
    {
        public readonly RingBuffer ProcessingTime = new RingBuffer();
        public readonly TaggedLongValueWriterV1 Writer = new TaggedLongValueWriterV1();

        void Write(long value, string tag, RingBuffer buffer)
        {
            const int maxAttempts = 10;

            if (buffer.TryWrite(value, Writer.GetTagId(tag)))
            {
                return;
            }

            var spin = new SpinWait();
            for (var i = 0; i < maxAttempts; i++)
            {
                spin.SpinOnce();
                if (buffer.TryWrite(value, Writer.GetTagId(tag)))
                {
                    return;
                }
            }
        }

        public void ReportProcessingTime(TimeSpan value, string messageType)
        {
            Write((long)value.TotalMilliseconds, messageType, ProcessingTime);
        }
    }
}