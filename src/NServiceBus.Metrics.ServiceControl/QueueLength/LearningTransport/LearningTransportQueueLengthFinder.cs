namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Settings;
    using Transport;
    
    class LearningTransportQueueLengthFinder 
    {
        public Task Warmup(ReadOnlySettings settings)
        {
            queueBindings = settings.Get<QueueBindings>();
            transportFolder = GetTransportFolder(settings);
            return Task.FromResult(0);
        }

        #region Transport Implementation Details

        const string StorageLocationKey = "LearningTransport.StoragePath";

        static string GetTransportFolder(ReadOnlySettings settings)
        {
            if (!settings.TryGet(StorageLocationKey, out string storagePath))
            {
                var solutionRoot = FindSolutionRoot();
                storagePath = Path.Combine(solutionRoot, ".learningtransport");
            }
            return storagePath;
        }

        static string FindSolutionRoot()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;

            while (true)
            {
                if (Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".sln")))
                {
                    return directory;
                }

                var parent = Directory.GetParent(directory);

                if (parent == null)
                {
                    // throw for now. if we discover there is an edge then we can fix it in a patch.
                    throw new Exception("Couldn't find the solution directory for the learning transport. If the endpoint is outside the solution folder structure, make sure to specify a storage directory using the 'EndpointConfiguration.UseTransport<LearningTransport>().StorageDirectory()' API.");
                }

                directory = parent.FullName;
            }
        }

        #endregion

        public Task UpdateQueueLength()
        {
            foreach (var queue in queueBindings.ReceivingAddresses)
            {
                var fullPath = Path.Combine(transportFolder, queue);
                var dirInfo = new DirectoryInfo(fullPath);

                if (dirInfo.Exists)
                {
                    var msgCount = dirInfo.EnumerateFiles().Count();
                    probe.Signal(queue, msgCount);
                }
            }

            return Task.FromResult(0);
        }

        QueueBindings queueBindings;
        string transportFolder;
        IQueueLengthProbe probe;

        public LearningTransportQueueLengthFinder(IQueueLengthProbe probe)
        {
            this.probe = probe;
        }
    }
}
