using System.Runtime.CompilerServices;
using NServiceBus.Metrics.ServiceControl;
using NServiceBus.Metrics.ServiceControl.Msmq.Tests;
using NUnit.Framework;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Approve()
    {
        var publicApi = ApiGenerator.GeneratePublicApi(typeof(BusConfigurationExtensions).Assembly);
        TestApprover.Verify(publicApi);
    }
}