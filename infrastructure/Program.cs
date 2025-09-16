using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.AzureNative.CognitiveServices;
using Pulumi.AzureNative.Authorization;
using System.Collections.Generic;

return await Pulumi.Deployment.RunAsync(() =>
{
    var rg = new ResourceGroup("dad-joke-rg");

    var stg = new StorageAccount("dadjokesa", new StorageAccountArgs
    {
        ResourceGroupName = rg.Name,
        Sku = new Pulumi.AzureNative.Storage.Inputs.SkuArgs { Name = SkuName.Standard_LRS },
        Kind = Kind.StorageV2,
        AllowBlobPublicAccess = false
    });

    // Connection string for Functions runtime
    var storageConn = Output.Tuple(rg.Name, stg.Name).Apply(async t =>
    {
        var (rgName, acctName) = t;
        var keys = await ListStorageAccountKeys.InvokeAsync(new ListStorageAccountKeysArgs
        {
            ResourceGroupName = rgName,
            AccountName = acctName
        });
        var key = keys.Keys[0].Value;
        return $"DefaultEndpointsProtocol=https;AccountName={acctName};AccountKey={key};EndpointSuffix=core.windows.net";
    });

    // Azure OpenAI (Cognitive Services)
    var openAI = new Account("dad-joke-openai", new AccountArgs
    {
        ResourceGroupName = rg.Name,
        Location = "eastus",
        Kind = "OpenAI",
        Sku = new Pulumi.AzureNative.CognitiveServices.Inputs.SkuArgs
        {
            Name = "S0"
        }
    });

    // Note: Azure OpenAI model deployment can be done manually in Azure Portal
    // Different regions support different models, so this is commented out for flexibility

    // Linux Consumption plan for Functions
    var plan = new AppServicePlan("dad-joke-plan", new AppServicePlanArgs
    {
        ResourceGroupName = rg.Name,
        Kind = "functionapp",
        Reserved = true,                  // IMPORTANT: Linux
        Sku = new SkuDescriptionArgs { Name = "Y1", Tier = "Dynamic" }
    });

    // Blob container to hold packages
    var container = new BlobContainer("packages", new BlobContainerArgs
    {
        AccountName = stg.Name,
        ResourceGroupName = rg.Name,
        PublicAccess = PublicAccess.None
    });

    // Upload function-app.zip to the container
    var packageBlob = new Blob("function-app.zip", new BlobArgs
    {
        AccountName = stg.Name,
        ResourceGroupName = rg.Name,
        ContainerName = container.Name,
        Type = BlobType.Block,
        Source = new Pulumi.FileAsset("../function/function-app.zip"),
        ContentType = "application/zip",
    });

    // Build a SAS URL for the container so the app can fetch the zip
    var packageSasUrl = Output.Tuple(stg.Name, rg.Name, container.Name, packageBlob.Name).Apply(async t =>
    {
        var (acct, rgName, cont, blob) = t;
        var sas = await ListStorageAccountServiceSAS.InvokeAsync(new ListStorageAccountServiceSASArgs
        {
            AccountName = acct,
            ResourceGroupName = rgName,
            Protocols = HttpProtocol.Https,
            SharedAccessStartTime = "2024-01-01",
            SharedAccessExpiryTime = "2050-01-01",
            Resource = SignedResource.C,     // container
            Permissions = Permissions.R,     // read
            CanonicalizedResource = $"/blob/{acct}/{cont}",
            ContentType = "application/zip",
        });
        return $"https://{acct}.blob.core.windows.net/{cont}/{blob}?{sas.ServiceSasToken}";
    });

    // Function App (Linux, .NET 8 isolated)
    var app = new WebApp("dad-joke-function", new WebAppArgs
    {
        ResourceGroupName = rg.Name,
        ServerFarmId = plan.Id,
        Kind = "functionapp,linux",
        HttpsOnly = true,
        Identity = new Pulumi.AzureNative.Web.Inputs.ManagedServiceIdentityArgs
        {
            Type = Pulumi.AzureNative.Web.ManagedServiceIdentityType.SystemAssigned
        },
        SiteConfig = new SiteConfigArgs
        {
            LinuxFxVersion = "DOTNET-ISOLATED|8.0",
            AppSettings = new[]
            {
                new NameValuePairArgs { Name = "FUNCTIONS_EXTENSION_VERSION", Value = "~4" },
                new NameValuePairArgs { Name = "FUNCTIONS_WORKER_RUNTIME",   Value = "dotnet-isolated" },
                new NameValuePairArgs { Name = "AzureWebJobsStorage",        Value = storageConn },
                new NameValuePairArgs { Name = "WEBSITE_RUN_FROM_PACKAGE",   Value = packageSasUrl },
                // Azure OpenAI configuration
                new NameValuePairArgs { Name = "AZURE_OPENAI_ENDPOINT", Value = "https://dad-joke-openai-custom.openai.azure.com/" },
                new NameValuePairArgs { Name = "AZURE_OPENAI_DEPLOYMENT_NAME", Value = "gpt-35-turbo" },
            },
            Http20Enabled = true,
        }
    });

    // Role assignment to give Function App access to Azure OpenAI
    var roleAssignment = new RoleAssignment("openai-access", new RoleAssignmentArgs
    {
        Scope = openAI.Id,
        RoleDefinitionId = "/subscriptions/0282681f-7a9e-424b-80b2-96babd57a8a1/providers/Microsoft.Authorization/roleDefinitions/5e0bd9bd-7b93-4f28-af87-19fc36ad61bd", // Cognitive Services OpenAI User
        PrincipalId = app.Identity.Apply(i => i!.PrincipalId!),
        PrincipalType = PrincipalType.ServicePrincipal
    });

    return new Dictionary<string, object?>
    {
        ["functionAppUrl"] = app.DefaultHostName.Apply(h => $"https://{h}"),
        ["jokeEndpoint"]   = app.DefaultHostName.Apply(h => $"https://{h}/api/joke"),
        ["openAIAccountName"] = openAI.Name
    };
});