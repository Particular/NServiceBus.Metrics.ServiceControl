﻿namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Threading;
    using global::ServiceControl.Monitoring.Data;

    class Buffer
    {
        public readonly RingBuffer Ring = new RingBuffer();
        public readonly TaggedLongValueWriterV1 Writer = new TaggedLongValueWriterV1();
    }

    class Buffers
    {
        public readonly Buffer ProcessingTime = new Buffer();
        public readonly Buffer CriticalTime = new Buffer();
        public readonly Buffer Retries = new Buffer();
        public readonly Buffer QueueLength = new Buffer();

        static void Write(long value, string tag, Buffer buffer)
        {
            const int maxAttempts = 10;

            var tagId = buffer.Writer.GetTagId(tag);
            if (buffer.Ring.TryWrite(value, tagId))
            {
                return;
            }

            var spin = new SpinWait();
            for (var i = 0; i < maxAttempts; i++)
            {
                spin.SpinOnce();
                if (buffer.Ring.TryWrite(value, tagId))
                {
                    return;
                }
            }
        }

        public void ReportProcessingTime(TimeSpan value, string messageType)
        {
            Write((long)value.TotalMilliseconds, messageType, ProcessingTime);
        }

        public void ReportCriticalTime(TimeSpan value, string messageType)
        {
            Write((long)value.TotalMilliseconds, messageType, CriticalTime);
        }

        public void ReportRetry(string messageType)
        {
            Write(1, messageType, Retries);
        }

        public void ReportQueueLength(long value, string queueName)
        {
            Write(value, queueName, QueueLength);
        }
    }
}