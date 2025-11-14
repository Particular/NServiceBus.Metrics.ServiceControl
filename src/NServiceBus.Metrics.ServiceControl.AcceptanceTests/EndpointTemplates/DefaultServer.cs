namespace NServiceBus.AcceptanceTests.EndpointTemplates;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AcceptanceTesting.Customization;
using AcceptanceTesting.Support;

public class DefaultServer : IEndpointSetupTemplate
{
    public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization)
    {
        var configuration = new EndpointConfiguration(endpointConfiguration.EndpointName);

        configuration.ScanTypesForTest(endpointConfiguration);

        var storageDir = Path.Combine(NServiceBusAcceptanceTest.StorageRootDir, NUnit.Framework.TestContext.CurrentContext.Test.ID);

        configuration.UseSerialization<SystemJsonSerializer>();
        configuration.UseTransport(new LearningTransport { StorageDirectory = storageDir });

        configuration.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

        await configurationBuilderCustomization(configuration);

        return configuration;
    }
}