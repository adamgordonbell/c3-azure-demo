using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using System.Collections.Generic;

return await Pulumi.Deployment.RunAsync(() =>
{
    // Create an Azure Resource Group
    var resourceGroup = new ResourceGroup("resourceGroup");

    // Create an Azure Storage Account with static website hosting
    var storageAccount = new StorageAccount("sa", new StorageAccountArgs
    {
        ResourceGroupName = resourceGroup.Name,
        Sku = new SkuArgs
        {
            Name = SkuName.Standard_LRS
        },
        Kind = Kind.StorageV2,
        AllowBlobPublicAccess = true
    });

    // Enable static website hosting
    var staticWebsite = new StorageAccountStaticWebsite("staticWebsite", new StorageAccountStaticWebsiteArgs
    {
        AccountName = storageAccount.Name,
        ResourceGroupName = resourceGroup.Name,
        IndexDocument = "index.html",
        Error404Document = "404.html"
    });

    // Upload a sample index.html file
    var indexHtml = new Blob("index.html", new BlobArgs
    {
        AccountName = storageAccount.Name,
        ResourceGroupName = resourceGroup.Name,
        ContainerName = "$web",
        Source = new FileAsset("index.html"),
        ContentType = "text/html"
    });

    // Get the primary web endpoint
    var webEndpoint = storageAccount.PrimaryEndpoints.Apply(endpoints => endpoints.Web);

    // Export the website URL and storage account info
    return new Dictionary<string, object?>
    {
        ["websiteUrl"] = webEndpoint,
        ["storageAccountName"] = storageAccount.Name
    };
});