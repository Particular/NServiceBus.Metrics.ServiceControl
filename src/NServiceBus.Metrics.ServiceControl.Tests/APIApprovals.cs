using NServiceBus.Metrics.ServiceControl;
using NUnit.Framework;
using Particular.Approvals;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    public void Approve()
    {
        var publicApi = ApiGenerator.GeneratePublicApi(typeof(BusConfigurationExtensions).Assembly);
        Approver.Verify(publicApi);
    }
}