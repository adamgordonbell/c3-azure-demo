using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.AI.OpenAI;
using System.Text.Json;
using Azure;

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
        return await HandleRequest(req, GenerateJoke);
    }

    [Function("TestConfig")]
    public async Task<HttpResponseData> TestConfig(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test")] HttpRequestData req)
    {
        return await HandleRequest(req, TestConfiguration);
    }

    private async Task<HttpResponseData> HandleRequest(HttpRequestData req, Func<string?, Task<object>> handler)
    {
        try
        {
            string? keywords = req.Query["keywords"];
            _logger.LogInformation("Processing request with keywords: {Keywords}", keywords ?? "none");

            if (_openAiClient == null)
            {
                _logger.LogError("Azure OpenAI client not configured");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Service temporarily unavailable" }));
                return errorResponse;
            }

            var result = await handler(keywords);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in request: {Message}", ex.Message);

            // Determine error type for user feedback
            var (userMessage, canContactAPI) = ex.Message.ToLower() switch
            {
                var msg when msg.Contains("unauthorized") || msg.Contains("403") || msg.Contains("401") =>
                    ("Authentication failed with OpenAI service", true),
                var msg when msg.Contains("timeout") || msg.Contains("network") || msg.Contains("connection") =>
                    ("Cannot reach OpenAI service", false),
                var msg when msg.Contains("quota") || msg.Contains("rate limit") =>
                    ("OpenAI service quota exceeded", true),
                _ => ("OpenAI service error", true)
            };

            var errorDetails = new
            {
                error = "Request failed",
                reason = userMessage,
                canContactAPI = canContactAPI
            };

            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(errorDetails));
            return errorResponse;
        }
    }

    private async Task<object> GenerateJoke(string? keywords)
    {
        var joke = await GenerateAIJoke(keywords);
        return new { joke = joke };
    }

    private async Task<object> TestConfiguration(string? keywords)
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        var hasApiKey = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"));

        return new
        {
            hasOpenAIClient = _openAiClient != null,
            endpoint = endpoint,
            deploymentName = deploymentName,
            hasApiKey = hasApiKey,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    private async Task<string> GenerateAIJoke(string? keywords)
    {
        var deploymentName = "gpt-4o-mini";
        _logger.LogInformation("Attempting OpenAI call with deployment: {Deployment}, hasClient: {HasClient}",
            deploymentName, _openAiClient != null);

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

        try
        {
            var response = await _openAiClient!.GetChatCompletionsAsync(chatRequest);
            var joke = response.Value.Choices[0].Message.Content?.Trim();

            if (string.IsNullOrEmpty(joke))
            {
                _logger.LogWarning("OpenAI returned empty content");
                throw new InvalidOperationException("AI service returned empty response");
            }

            return joke;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API call failed. Error type: {Type}, Message: {Message}, Stack: {Stack}",
                ex.GetType().Name, ex.Message, ex.StackTrace);

            // Log additional details for Azure.RequestFailedException
            if (ex is Azure.RequestFailedException azEx)
            {
                _logger.LogError("Azure Request Failed - Status: {Status}, ErrorCode: {Code}, Details: {Details}",
                    azEx.Status, azEx.ErrorCode, azEx.ToString());
            }

            throw;
        }
    }

}