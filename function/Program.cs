using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Functions.Worker;
using Azure.AI.OpenAI;
using Azure.Identity;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Add Azure OpenAI client
        var openAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        if (!string.IsNullOrEmpty(openAiEndpoint))
        {
            services.AddSingleton(new OpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential()));
        }
    })
    .Build();

host.Run();