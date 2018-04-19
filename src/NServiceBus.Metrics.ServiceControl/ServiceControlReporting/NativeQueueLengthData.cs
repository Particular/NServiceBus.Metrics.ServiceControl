namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    class NativeQueueLengthData
    {
        string inputQueue;

        public NativeQueueLengthData(string inputQueue)
        {
            this.inputQueue = inputQueue;
        }

        public string ToJson()
        {
            return SimpleJson.SerializeObject(new {InputQueue = inputQueue});
        }
    }
}