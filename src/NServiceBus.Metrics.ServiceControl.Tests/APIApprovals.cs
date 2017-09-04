namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using ApprovalTests;
    using ApprovalTests.Core;
    using ApprovalTests.Writers;
    using NUnit.Framework;
    using PublicApiGenerator;

    [TestFixture]
    public class APIApprovals
    {
        static readonly Assembly Assembly = typeof(ReportingConfigurationExtensions).Assembly;

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Approve()
        {
            var publicApi = ApiGenerator.GeneratePublicApi(Assembly);
            Approvals.Verify(WriterFactory.CreateTextWriter(publicApi, "cs"), GetNamer(), Approvals.GetReporter());
        }

        static IApprovalNamer GetNamer([CallerFilePath] string path = "")
        {
            var dir = Path.GetDirectoryName(path);
            var name = Assembly.GetName().Name;

            return new Namer
            {
                Name = name,
                SourcePath = dir,
            };
        }

        class Namer : IApprovalNamer
        {
            public string SourcePath { get; set; }
            public string Name { get; set; }
        }
    }
}