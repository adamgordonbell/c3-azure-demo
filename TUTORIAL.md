# Dad Joke Function App - Complete Tutorial

A step-by-step tutorial to build, test, and deploy a serverless Azure Function that generates AI-powered dad jokes with usage tracking.

## 🎯 What You'll Build

By the end of this tutorial, you'll have:
- A working Azure Function App with two HTTP endpoints
- AI-powered joke generation using OpenAI API
- Usage statistics and joke history storage
- Complete Azure infrastructure managed with Pulumi
- Local development environment for testing

## 📋 Prerequisites

Before starting, install these tools:

```bash
# Install .NET 8
brew install --cask dotnet@8

# Install Azure CLI
brew install azure-cli

# Install Pulumi CLI
brew install pulumi

# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

### Environment Setup

```bash
# Authenticate with Azure
az login

# Set DOTNET_ROOT for .NET 8 (add to ~/.zshrc permanently)
export DOTNET_ROOT=/opt/homebrew/Cellar/dotnet@8/8.0.120/libexec

# Verify installations
dotnet --version          # Should show 8.x
az --version             # Should show Azure CLI info
pulumi version           # Should show Pulumi version
func --version           # Should show 4.x
```

## 🏗️ Step 1: Set Up Project Structure

Create the project directory structure:

```bash
mkdir dad-joke-function
cd dad-joke-function

# Create subdirectories
mkdir infrastructure function scripts

# Initialize git repository
git init
```

## 🔧 Step 2: Build the Infrastructure (Pulumi)

### Create Infrastructure Project

```bash
cd infrastructure

# Initialize Pulumi project
pulumi new azure-csharp --name dad-joke-infrastructure --stack dev
# Follow prompts, set location to your preferred region (e.g., canadacentral)
```

### Configure Infrastructure Code

Create `infrastructure/Program.cs`:

```csharp
using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
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
        SiteConfig = new SiteConfigArgs
        {
            LinuxFxVersion = "DOTNET-ISOLATED|8.0",
            AppSettings = new[]
            {
                new NameValuePairArgs { Name = "FUNCTIONS_EXTENSION_VERSION", Value = "~4" },
                new NameValuePairArgs { Name = "FUNCTIONS_WORKER_RUNTIME",   Value = "dotnet-isolated" },
                new NameValuePairArgs { Name = "AzureWebJobsStorage",        Value = storageConn },
                new NameValuePairArgs { Name = "WEBSITE_RUN_FROM_PACKAGE",   Value = packageSasUrl },
                new NameValuePairArgs { Name = "Http20Enabled",              Value = "true" },
            },
        }
    });

    // Tables for joke storage
    var jokesTable = new Table("jokes", new TableArgs
    {
        ResourceGroupName = rg.Name,
        AccountName = stg.Name,
        TableName = "jokes"
    });

    var statsTable = new Table("stats", new TableArgs
    {
        ResourceGroupName = rg.Name,
        AccountName = stg.Name,
        TableName = "stats"
    });

    return new Dictionary<string, object?>
    {
        ["functionAppUrl"] = app.DefaultHostName.Apply(h => $"https://{h}"),
        ["jokeEndpoint"]   = app.DefaultHostName.Apply(h => $"https://{h}/api/joke"),
        ["statsEndpoint"]  = app.DefaultHostName.Apply(h => $"https://{h}/api/stats"),
    };
});
```

## 💻 Step 3: Build the Azure Function

### Create Function Project

```bash
cd ../function

# Create project file
cat > DadJokeFunctionApp.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="1.20.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="1.16.4" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" Version="3.1.0" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
    <PackageReference Include="Azure.Data.Tables" Version="12.8.3" />
    <PackageReference Include="Azure.Security.KeyVault.Secrets" Version="4.5.0" />
    <PackageReference Include="Azure.Identity" Version="1.12.1" />
    <PackageReference Include="System.Net.Http.Json" Version="8.0.0" />
  </ItemGroup>
  <ItemGroup>
    <None Update="host.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="local.settings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>Never</CopyToPublishDirectory>
    </None>
  </ItemGroup>
</Project>
EOF
```

### Create Function Configuration Files

Create `function/host.json`:

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request"
      },
      "enableLiveMetricsFilters": true
    }
  }
}
```

Create `function/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "OPENAI_API_KEY": "your-openai-api-key-here"
  }
}
```

### Create Program.cs

Create `function/Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Functions.Worker;
using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddHttpClient();

        var storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (!string.IsNullOrEmpty(storageConnectionString))
        {
            services.AddSingleton(new TableServiceClient(storageConnectionString));
        }

        var keyVaultUrl = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URL");
        if (!string.IsNullOrEmpty(keyVaultUrl))
        {
            services.AddSingleton(new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential()));
        }
    })
    .Build();

host.Run();
```

### Create Function Implementation

Create `function/JokeFunctions.cs` - **This is a large file, see the full implementation in the project**

The key functions are:
- `GetJoke`: Generates dad jokes with optional keywords
- `GetStats`: Returns usage statistics and recent jokes

## 🧪 Step 4: Test Locally

### Build and Start the Function

```bash
cd function

# Restore packages
dotnet restore

# Build the project
dotnet build

# Start the Azure Functions runtime
func start --port 7071
```

**Expected Output:**
```
Functions:
    GetJoke: [GET,POST] http://localhost:7071/api/joke
    GetStats: [GET] http://localhost:7071/api/stats
```

### Test the Endpoints

In a new terminal, test both endpoints:

```bash
# Test joke endpoint
curl http://localhost:7071/api/joke

# Expected Response:
# {
#   "joke": "Why did the scarecrow win an award? He was outstanding in his field!",
#   "keywords": null,
#   "requestCount": 1,
#   "timestamp": "2024-01-15T10:30:00Z"
# }

# Test with keywords
curl "http://localhost:7071/api/joke?keywords=programming"

# Expected Response:
# {
#   "joke": "Why do programmers prefer dark mode? Because light attracts bugs!",
#   "keywords": "programming",
#   "requestCount": 2,
#   "timestamp": "2024-01-15T10:31:00Z"
# }

# Test stats endpoint
curl http://localhost:7071/api/stats

# Expected Response (will show error locally due to no storage):
# {
#   "totalRequests": 0,
#   "todayRequests": 0,
#   "recentJokes": [],
#   "error": "Failed to retrieve statistics"
# }
```

### Local Testing Observations

✅ **What Works Locally:**
- Function discovery and registration
- HTTP endpoints responding correctly
- OpenAI API integration (if API key is provided)
- JSON response formatting
- Error handling

⚠️ **Expected Local Limitations:**
- Table Storage operations fail (no local Azure Storage Emulator)
- Statistics tracking won't persist
- Functions fall back to hardcoded jokes if no OpenAI key

**This is normal and expected for local development!**

## 🚀 Step 5: Deploy to Azure

### Prepare Function Package

First, build and package the function code:

```bash
cd function

# Build and package the function code
dotnet publish -c Release -o bin/Release/net8.0/publish
cd bin/Release/net8.0/publish
zip -r ../../../function-app.zip .
cd ../../../../infrastructure
```

**This creates `function-app.zip` that Pulumi will deploy automatically.**

### Pure Pulumi Deployment

Everything now happens with one Pulumi command - no Azure CLI needed:

```bash
# Preview what will be created (infrastructure + function deployment)
pulumi preview

# Deploy infrastructure and function code together
pulumi up
# Type 'yes' when prompted

# Get the deployment outputs
pulumi stack output
```

**Expected Output:**
```
Current stack outputs (3):
    OUTPUT              VALUE
    functionAppUrl      https://dad-joke-function[hash].azurewebsites.net
    jokeEndpoint        https://dad-joke-function[hash].azurewebsites.net/api/joke
    statsEndpoint       https://dad-joke-function[hash].azurewebsites.net/api/stats
```

**What happens during deployment:**
1. ✅ Creates all Azure infrastructure (Function App, Storage, Tables)
2. ✅ Uploads `function-app.zip` to blob storage
3. ✅ Generates secure SAS URL for package access
4. ✅ Configures Function App to run from the package
5. ✅ Returns working endpoint URLs

**🎯 Pure Infrastructure as Code** - No `func` CLI or manual deployment steps!

### Configure OpenAI API Key (Optional)

To enable AI-powered jokes, add your OpenAI API key to the function's environment:

```bash
# You can add environment variables to the Function App in Azure Portal
# Or modify the Pulumi code to include:
# new NameValuePairArgs { Name = "OPENAI_API_KEY", Value = "your-key" }
```

**Note**: The function gracefully falls back to hardcoded jokes if no OpenAI key is configured.

## 🧪 Step 6: Test Deployed Function

### Test the Live Endpoints

```bash
# Navigate back to infrastructure directory
cd infrastructure

# Test the deployed endpoints using Pulumi outputs
curl "$(pulumi stack output jokeEndpoint)"

# Expected Response:
# {
#   "joke": "Why don't scientists trust atoms? Because they make up everything!",
#   "keywords": null,
#   "requestCount": 1,
#   "timestamp": "2025-09-16T15:31:40.2672097Z"
# }

# Test with keywords
curl "$(pulumi stack output jokeEndpoint)?keywords=azure"

# Test stats endpoint (should work now with real Azure Table Storage)
curl "$(pulumi stack output statsEndpoint)"

# Expected Response:
# {
#   "totalRequests": 1,
#   "todayRequests": 1,
#   "recentJokes": [
#     {
#       "joke": "Why don't scientists trust atoms? Because they make up everything!",
#       "keywords": "",
#       "timestamp": "2025-09-16T15:31:40.2258639Z"
#     }
#   ]
# }
```

### Quick Test Commands

```bash
# Get ready-to-use curl commands
echo "Joke endpoint: curl \"$(pulumi stack output jokeEndpoint)\""
echo "Stats endpoint: curl \"$(pulumi stack output statsEndpoint)\""

# Test with keywords
echo "With keywords: curl \"$(pulumi stack output jokeEndpoint)?keywords=programming\""
```

## 📊 Step 7: Monitor and Observe

### View Function Logs

```bash
# Stream function logs
func azure functionapp logstream $FUNCTION_APP_NAME
```

### Check Application Insights

1. Go to Azure Portal
2. Navigate to your resource group
3. Open Application Insights resource
4. View Live Metrics, Logs, and Performance data

## 🧹 Step 8: Clean Up

When you're done experimenting:

```bash
cd infrastructure
pulumi destroy
# Type 'yes' when prompted to delete all resources
```

## 🎯 What You've Accomplished

✅ **Pure Infrastructure as Code**: Complete Azure infrastructure with Pulumi
✅ **Zip-Based Deployment**: Function code deployed via blob storage + SAS URL
✅ **Linux Serverless Functions**: Modern .NET 8 isolated Azure Functions
✅ **AI Integration**: OpenAI-powered joke generation with fallbacks
✅ **Data Persistence**: Table Storage for jokes and statistics
✅ **No Manual Steps**: Fully automated deployment without Azure CLI
✅ **Local Development**: Complete local testing environment

## 💰 Cost Breakdown

**Monthly costs for moderate usage (1K requests/day):**
- Azure Functions (Consumption): ~$1
- Storage Account: ~$1
- Application Insights: Free tier
- Key Vault: ~$1

**Total: ~$3/month**

## 🔄 Next Steps

Consider adding:
- Caching layer (Redis) for performance
- Rate limiting per user/IP
- Multiple joke categories
- Web frontend interface
- Slack/Teams integration webhooks
- A/B testing for different joke styles

## 🐛 Common Issues

**Function not found locally:**
- Ensure `FUNCTIONS_WORKER_RUNTIME` is set to `dotnet-isolated`
- Verify .NET 8 is installed and `DOTNET_ROOT` is set

**OpenAI API errors:**
- Check API key is valid and has credits
- Function falls back to hardcoded jokes on API failures

**Deployment fails:**
- Ensure you're logged into Azure CLI (`az login`)
- Check you have Contributor permissions on the subscription
- Verify all required tools are installed

**Storage errors locally:**
- This is expected - Azure Storage Emulator is not running
- Functions handle this gracefully and return mock data

## 🔄 Complete Workflow Summary

Here's the entire deployment process from start to finish:

```bash
# 1. Build and package function code
cd function
dotnet publish -c Release -o bin/Release/net8.0/publish
cd bin/Release/net8.0/publish && zip -r ../../../function-app.zip . && cd ../../../../infrastructure

# 2. Deploy everything with Pulumi
pulumi up --yes

# 3. Test the deployed functions immediately
curl "$(pulumi stack output jokeEndpoint)"
curl "$(pulumi stack output statsEndpoint)"
```

**That's it!** Just 3 commands for complete deployment. The new approach:
- ✅ **Pure Pulumi** - No Azure CLI function deployment needed
- ✅ **Zip-based deployment** - Upload to blob storage with SAS URL
- ✅ **Linux serverless** - Modern container-based Function Apps
- ✅ **WEBSITE_RUN_FROM_PACKAGE** - Secure, efficient code deployment
- ✅ **Infrastructure as Code** - Everything versioned and reproducible

## 🎯 Expected Results

After following the complete tutorial, you should have:

✅ **Working Local Development**: Functions running at `http://localhost:7071/api/joke` and `/api/stats`

✅ **Deployed Azure Infrastructure**: 8 Azure resources including Function App, Storage, Key Vault

✅ **Live API Endpoints**:
- `https://[your-function-app].azurewebsites.net/api/joke`
- `https://[your-function-app].azurewebsites.net/api/stats`

✅ **Full Functionality**: AI-powered jokes, usage tracking, persistent storage

✅ **Pulumi Outputs**: Easy access to all endpoints via `pulumi stack output`

---

🎉 **Congratulations!** You've built a complete serverless application with AI integration, infrastructure automation, and monitoring. This pattern can be adapted for many other use cases beyond dad jokes!