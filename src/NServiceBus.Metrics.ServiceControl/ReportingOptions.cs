namespace NServiceBus.Metrics.ServiceControl;

using System;

class ReportingOptions
{
    internal string ServiceControlMetricsAddress;
    internal TimeSpan ServiceControlReportingInterval;
    internal string EndpointInstanceIdOverride;
    internal TimeSpan TimeToBeReceived = TimeSpan.FromDays(7);
}