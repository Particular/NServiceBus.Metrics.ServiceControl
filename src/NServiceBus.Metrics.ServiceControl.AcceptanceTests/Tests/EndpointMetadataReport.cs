﻿namespace NServiceBus.Metrics
{
    public class EndpointMetadataReport : IMessage
    {
        public string LocalAddress { get; set; }
        public int PluginVersion { get; set; }
    }
}