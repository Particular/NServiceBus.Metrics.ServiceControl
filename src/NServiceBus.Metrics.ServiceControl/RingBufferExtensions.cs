using NServiceBus.Logging;
using ServiceControl.Monitoring.Data;

class RingBufferExtensions
{
    const int MaxExpectedWriteAttempts = 10;
    public static void WriteTaggedValue(RingBuffer buffer, string metricType, long value, int tag)
    {
        var written = false;
        var attempts = 0;

        while (!written)
        {
            written = buffer.TryWrite(value, tag);

            attempts++;

            if (attempts >= MaxExpectedWriteAttempts)
            {
                log.Warn($"Failed to buffer metrics data for ${metricType} after ${attempts} attempts.");
                attempts = 0;
            }
        }
    }

    static readonly ILog log = LogManager.GetLogger<RingBufferExtensions>();
}