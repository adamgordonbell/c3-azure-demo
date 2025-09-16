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
        // Add HttpClient for LLM API calls
        services.AddHttpClient();

        // Add Azure Table Storage
        var storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (!string.IsNullOrEmpty(storageConnectionString))
        {
            services.AddSingleton(new TableServiceClient(storageConnectionString));
        }

        // Add Key Vault client
        var keyVaultUrl = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URL");
        if (!string.IsNullOrEmpty(keyVaultUrl))
        {
            services.AddSingleton(new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential()));
        }
    })
    .Build();

host.Run();