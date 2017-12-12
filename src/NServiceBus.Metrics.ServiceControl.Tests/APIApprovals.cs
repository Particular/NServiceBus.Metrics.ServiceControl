using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ApprovalTests;
using ApprovalTests.Core;
using NUnit.Framework;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Approve()
    {
        var combine = Path.Combine(TestContext.CurrentContext.TestDirectory, "NServiceBus.Metrics.ServiceControl.dll");
        var assembly = Assembly.LoadFile(combine);
        var publicApi = Filter(ApiGenerator.GeneratePublicApi(assembly));
        Approvals.Verify(BuildWriter(publicApi));
    }

    static IApprovalWriter BuildWriter(string api,[CallerFilePath] string path = null)
    {
        var directory = Path.GetDirectoryName(path);
        return new LocalApprovalTextWriter(api, "cs", directory);
    }

    string Filter(string text)
    {
        return string.Join(Environment.NewLine, text.Split(new[]
            {
                Environment.NewLine
            }, StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith("[assembly: System.Runtime.Versioning"))
            .Where(l => !string.IsNullOrWhiteSpace(l))
        );
    }

    class LocalApprovalTextWriter : ApprovalTextWriter
    {
        readonly string directory;

        public LocalApprovalTextWriter(string data, string extensionWithoutDot, string directory) 
            : base(data, extensionWithoutDot)
        {
            this.directory = directory;
        }

        public override string GetApprovalFilename(string basename) => Path.Combine(directory, base.GetApprovalFilename(basename));
        public override string GetReceivedFilename(string basename) => Path.Combine(directory, base.GetReceivedFilename(basename));
    }
}