namespace ServiceControl.Monitoring.Messaging
{
    using System.Text;

    public class TaggedLongValueOccurrence : RawMessage
    {
        public string TagValue { get; set; }

        public bool TryRecord(long dateTicks, long value)
        {
            if (IsFull)
            {
                return false;
            }

            Entries[Index].DateTicks = dateTicks;
            Entries[Index].Value = value;

            Index += 1;

            return true;
        }

        // NOTE: Only for testing
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(TagValue);
            sb.AppendLine($"Entries ({Entries.Length}):");
            foreach (var entry in Entries)
            {
                sb.AppendLine($"\t{entry.DateTicks}: {entry.Value}");
            }
            return sb.ToString();
        }
    }
}