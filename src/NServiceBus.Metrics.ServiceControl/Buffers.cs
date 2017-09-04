namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Threading;
    using global::ServiceControl.Monitoring.Data;

    class Buffers
    {
        public readonly RingBuffer ProcessingTimeBuffer = new RingBuffer();
        public readonly TaggedLongValueWriterV1 ProcessingTimeWriter = new TaggedLongValueWriterV1();

        void Write(long value, string tag, RingBuffer buffer, Func<string, int> tagProvider)
        {
            const int maxAttempts = 10;

            if (buffer.TryWrite(value, tagProvider(tag)))
            {
                return;
            }

            var spin = new SpinWait();
            for (var i = 0; i < maxAttempts; i++)
            {
                spin.SpinOnce();
                if (buffer.TryWrite(value, tagProvider(tag)))
                {
                    return;
                }
            }
        }

        public void ReportProcessingTime(TimeSpan value, string messageType)
        {
            Write((long)value.TotalMilliseconds, messageType, ProcessingTimeBuffer, ProcessingTimeWriter.GetTagId);
        }
    }
}