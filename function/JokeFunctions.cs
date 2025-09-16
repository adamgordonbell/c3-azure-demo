using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.AI.OpenAI;
using System.Text.Json;

namespace DadJokeFunctionApp;

public class JokeFunctions
{
    private readonly ILogger _logger;
    private readonly OpenAIClient? _openAiClient;

    public JokeFunctions(ILoggerFactory loggerFactory, OpenAIClient? openAiClient = null)
    {
        _logger = loggerFactory.CreateLogger<JokeFunctions>();
        _openAiClient = openAiClient;
    }

    [Function("GetJoke")]
    public async Task<HttpResponseData> GetJoke(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "joke")] HttpRequestData req)
    {
        _logger.LogInformation("Joke request received");

        try
        {
            string? keywords = req.Query["keywords"];
            _logger.LogInformation($"Keywords: {keywords ?? "none"}");

            if (_openAiClient == null)
            {
                _logger.LogError("Azure OpenAI client is null - environment variables not configured");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json");
                var error = new { error = "Azure OpenAI not configured" };
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(error));
                return errorResponse;
            }

            _logger.LogInformation("Calling GenerateAIJoke");
            var joke = await GenerateAIJoke(keywords);
            _logger.LogInformation($"Generated joke: {joke}");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var result = new
            {
                joke = joke,
                keywords = keywords,
                timestamp = DateTime.UtcNow
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating joke: {Message}", ex.Message);

            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");

            var error = new { error = "Failed to generate joke", details = ex.Message };
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(error));
            return errorResponse;
        }
    }

    private async Task<string> GenerateAIJoke(string? keywords)
    {
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-35-turbo";
        _logger.LogInformation($"Using deployment name: {deploymentName}");

        var prompt = string.IsNullOrEmpty(keywords)
            ? "Generate a clean, family-friendly dad joke. Just return the joke, nothing else."
            : $"Generate a clean, family-friendly dad joke about {keywords}. Just return the joke, nothing else.";

        _logger.LogInformation($"Prompt: {prompt}");

        var chatRequest = new ChatCompletionsOptions(deploymentName, new[]
        {
            new ChatRequestUserMessage(prompt)
        })
        {
            MaxTokens = 100,
            Temperature = 0.9f
        };

        _logger.LogInformation("Making Azure OpenAI API call");
        var response = await _openAiClient!.GetChatCompletionsAsync(chatRequest);
        _logger.LogInformation($"Response received with {response.Value.Choices.Count} choices");

        var joke = response.Value.Choices[0].Message.Content?.Trim();
        _logger.LogInformation($"Raw joke from API: {joke}");

        if (string.IsNullOrEmpty(joke))
        {
            throw new InvalidOperationException("Azure OpenAI returned empty joke content");
        }

        return joke;
    }

}