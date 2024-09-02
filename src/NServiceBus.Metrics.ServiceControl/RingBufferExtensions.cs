namespace NServiceBus.Metrics.ServiceControl
{
    using System.Threading;
    using NServiceBus.Logging;

    static class RingBufferExtensions
    {
        // SpinWait.SpinOnce will start to yield at about 10 spinning iterations, so give it a few more tries before logging errors
        const int MaxExpectedWriteAttempts = 15;

        public static void WriteTaggedValue(this RingBuffer buffer, string metricType, long value, int tag)
        {
            var spinWait = new SpinWait();
            var attempts = 0;

            while (true)
            {
                if (buffer.TryWrite(value, tag))
                {
                    return;
                }

                attempts++;

                if (attempts >= MaxExpectedWriteAttempts)
                {
                    Log.Warn($"Thread {Thread.CurrentThread.ManagedThreadId} failed to buffer metrics data for '{metricType}' after {attempts} attempts.");
                    attempts = 0;
                }

                // Ensure we don't block the CPU and prevent consumers of the RingBuffer to drain the Buffer
                spinWait.SpinOnce();
            }
        }

        static readonly ILog Log = LogManager.GetLogger(typeof(RingBufferExtensions));
    }
}