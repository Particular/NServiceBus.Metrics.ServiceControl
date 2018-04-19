using System.Collections.Generic;

class QueueLengthReport
{
    public long TimeStamp { get; set; }
    public Dictionary<string, long?> Queues { get; } = new Dictionary<string, long?>();
}
