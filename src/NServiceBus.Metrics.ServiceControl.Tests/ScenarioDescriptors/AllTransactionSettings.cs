﻿namespace NServiceBus.Metrics.ServiceControl.Tests.ScenarioDescriptors
{
    using AcceptanceTesting.Support;

    public class AllTransactionSettings : ScenarioDescriptor
    {
        public AllTransactionSettings()
        {
            Add(TransactionSettings.DistributedTransaction);
            Add(TransactionSettings.LocalTransaction);
            Add(TransactionSettings.NoTransaction);
        }
    }
}