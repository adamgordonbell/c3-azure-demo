using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Functions.Worker;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Add Azure OpenAI client
        var openAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var openAiApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        if (!string.IsNullOrEmpty(openAiEndpoint))
        {
            if (!string.IsNullOrEmpty(openAiApiKey))
            {
                // Use API key authentication
                services.AddSingleton(new OpenAIClient(new Uri(openAiEndpoint), new Azure.AzureKeyCredential(openAiApiKey)));
            }
            else
            {
                // Use Managed Identity authentication
                services.AddSingleton(new OpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential()));
            }
        }
    })
    .Build();

host.Run();