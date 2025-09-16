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
        try
        {
            string? keywords = req.Query["keywords"];

            if (_openAiClient == null)
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Azure OpenAI not configured" }));
                return errorResponse;
            }

            var joke = await GenerateAIJoke(keywords);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { joke = joke }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating joke");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Failed to generate joke" }));
            return errorResponse;
        }
    }

    private async Task<string> GenerateAIJoke(string? keywords)
    {
        var deploymentName = "gpt-4o-mini";

        var prompt = string.IsNullOrEmpty(keywords)
            ? "Generate a clean, family-friendly joke. Just return the joke, nothing else."
            : $"Generate a clean, family-friendly joke about {keywords}. Just return the joke, nothing else.";

        var chatRequest = new ChatCompletionsOptions(deploymentName, new[]
        {
            new ChatRequestUserMessage(prompt)
        })
        {
            MaxTokens = 100,
            Temperature = 0.9f
        };

        var response = await _openAiClient!.GetChatCompletionsAsync(chatRequest);
        var joke = response.Value.Choices[0].Message.Content?.Trim();

        if (string.IsNullOrEmpty(joke))
        {
            throw new InvalidOperationException("Azure OpenAI returned empty joke content");
        }

        return joke;
    }

}