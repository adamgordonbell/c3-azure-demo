using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using Azure;

namespace DadJokeFunctionApp;

public class JokeFunctions
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly TableServiceClient? _tableServiceClient;
    private readonly SecretClient? _secretClient;

    public JokeFunctions(ILoggerFactory loggerFactory, HttpClient httpClient,
        TableServiceClient? tableServiceClient = null, SecretClient? secretClient = null)
    {
        _logger = loggerFactory.CreateLogger<JokeFunctions>();
        _httpClient = httpClient;
        _tableServiceClient = tableServiceClient;
        _secretClient = secretClient;
    }

    [Function("GetJoke")]
    public async Task<HttpResponseData> GetJoke(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "joke")] HttpRequestData req)
    {
        _logger.LogInformation("Dad joke request received");

        try
        {
            // Extract keywords from query or body
            var keywords = await ExtractKeywords(req);

            // Generate joke using LLM
            var joke = await GenerateJoke(keywords);

            // Store joke in table storage
            var requestCount = await StoreJoke(joke, keywords);

            // Return response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var result = new
            {
                joke = joke,
                keywords = keywords,
                requestCount = requestCount,
                timestamp = DateTime.UtcNow
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating joke");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Failed to generate joke" }));
            return errorResponse;
        }
    }

    [Function("GetStats")]
    public async Task<HttpResponseData> GetStats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "stats")] HttpRequestData req)
    {
        _logger.LogInformation("Stats request received");

        try
        {
            var stats = await GetJokeStatistics();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(stats));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stats");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Failed to retrieve stats" }));
            return errorResponse;
        }
    }

    private async Task<string?> ExtractKeywords(HttpRequestData req)
    {
        // Try query parameter first
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var keywords = query["keywords"];

        if (!string.IsNullOrEmpty(keywords))
            return keywords;

        // Try request body for POST requests
        if (req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            var body = await req.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    var requestData = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                    if (requestData?.ContainsKey("keywords") == true)
                    {
                        return requestData["keywords"]?.ToString();
                    }
                }
                catch
                {
                    // If JSON parsing fails, treat the whole body as keywords
                    return body.Trim('"');
                }
            }
        }

        return null;
    }

    private async Task<string> GenerateJoke(string? keywords)
    {
        // For demo purposes, use a simple approach first
        // In production, you'd call OpenAI API or Azure OpenAI

        var prompt = string.IsNullOrEmpty(keywords)
            ? "Tell me a clean dad joke"
            : $"Tell me a clean dad joke that incorporates these keywords: {keywords}";

        // Try to get OpenAI API key from Key Vault or environment
        var apiKey = await GetOpenAiApiKey();

        if (string.IsNullOrEmpty(apiKey))
        {
            // Fallback to hardcoded jokes for demo
            return GenerateFallbackJoke(keywords);
        }

        try
        {
            return await CallOpenAiApi(prompt, apiKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call OpenAI API, using fallback");
            return GenerateFallbackJoke(keywords);
        }
    }

    private async Task<string?> GetOpenAiApiKey()
    {
        // Try Key Vault first
        if (_secretClient != null)
        {
            try
            {
                var secret = await _secretClient.GetSecretAsync("openai-api-key");
                return secret.Value.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get API key from Key Vault");
            }
        }

        // Fallback to environment variable
        return Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }

    private async Task<string> CallOpenAiApi(string prompt, string apiKey)
    {
        var requestData = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "system", content = "You are a dad joke comedian. Always return clean, family-friendly dad jokes. Keep responses under 200 characters." },
                new { role = "user", content = prompt }
            },
            max_tokens = 100,
            temperature = 0.8
        };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", requestData);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return result.GetProperty("choices")[0]
                   .GetProperty("message")
                   .GetProperty("content")
                   .GetString() ?? "Why don't scientists trust atoms? Because they make up everything!";
    }

    private string GenerateFallbackJoke(string? keywords)
    {
        var jokes = new[]
        {
            "Why don't scientists trust atoms? Because they make up everything!",
            "I told my wife she was drawing her eyebrows too high. She looked surprised.",
            "Why did the scarecrow win an award? He was outstanding in his field!",
            "What do you call a fake noodle? An impasta!",
            "Why don't eggs tell jokes? They'd crack each other up!",
            "What's the best thing about Switzerland? I don't know, but the flag is a big plus.",
            "Why did the coffee file a police report? It got mugged!",
            "What do you call a bear with no teeth? A gummy bear!"
        };

        // If keywords provided, try to find a somewhat relevant joke
        if (!string.IsNullOrEmpty(keywords))
        {
            var keywordLower = keywords.ToLower();
            if (keywordLower.Contains("science") || keywordLower.Contains("atom"))
                return jokes[0];
            if (keywordLower.Contains("food") || keywordLower.Contains("noodle"))
                return jokes[3];
            if (keywordLower.Contains("coffee") || keywordLower.Contains("drink"))
                return jokes[6];
        }

        // Return random joke
        var random = new Random();
        return jokes[random.Next(jokes.Length)];
    }

    private async Task<int> StoreJoke(string joke, string? keywords)
    {
        if (_tableServiceClient == null)
        {
            _logger.LogWarning("Table service client not available, cannot store joke");
            return 1; // Return dummy count
        }

        try
        {
            var tableClient = _tableServiceClient.GetTableClient("jokes");
            await tableClient.CreateIfNotExistsAsync();

            // Store the joke
            var jokeEntity = new TableEntity("joke", Guid.NewGuid().ToString())
            {
                ["JokeText"] = joke,
                ["Keywords"] = keywords ?? "",
                ["Timestamp"] = DateTime.UtcNow,
                ["Date"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
            };

            await tableClient.AddEntityAsync(jokeEntity);

            // Update/increment request count
            var statsTableClient = _tableServiceClient.GetTableClient("stats");
            await statsTableClient.CreateIfNotExistsAsync();

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var statsEntity = new TableEntity("daily", today)
            {
                ["Count"] = 1,
                ["LastUpdated"] = DateTime.UtcNow
            };

            try
            {
                // Try to get existing count
                var existing = await statsTableClient.GetEntityAsync<TableEntity>("daily", today);
                var currentCount = existing.Value.GetInt32("Count") ?? 0;
                statsEntity["Count"] = currentCount + 1;
                await statsTableClient.UpdateEntityAsync(statsEntity, existing.Value.ETag);
                return currentCount + 1;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Entity doesn't exist, create new
                await statsTableClient.AddEntityAsync(statsEntity);
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store joke in table storage");
            return 1; // Return dummy count
        }
    }

    private async Task<object> GetJokeStatistics()
    {
        if (_tableServiceClient == null)
        {
            return new
            {
                totalRequests = 0,
                todayRequests = 0,
                recentJokes = new string[0],
                message = "Table storage not available"
            };
        }

        try
        {
            var jokesTableClient = _tableServiceClient.GetTableClient("jokes");
            var statsTableClient = _tableServiceClient.GetTableClient("stats");

            // Get today's count
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var todayCount = 0;
            try
            {
                var todayStats = await statsTableClient.GetEntityAsync<TableEntity>("daily", today);
                todayCount = todayStats.Value.GetInt32("Count") ?? 0;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // No requests today
            }

            // Get total count (sum of all daily counts)
            var totalCount = 0;
            await foreach (var entity in statsTableClient.QueryAsync<TableEntity>(filter: "PartitionKey eq 'daily'"))
            {
                totalCount += entity.GetInt32("Count") ?? 0;
            }

            // Get recent jokes (last 5)
            var recentJokes = new List<object>();
            await foreach (var entity in jokesTableClient.QueryAsync<TableEntity>(
                filter: "PartitionKey eq 'joke'",
                maxPerPage: 5))
            {
                recentJokes.Add(new
                {
                    joke = entity.GetString("JokeText"),
                    keywords = entity.GetString("Keywords"),
                    timestamp = entity.GetDateTime("Timestamp")
                });

                if (recentJokes.Count >= 5) break;
            }

            return new
            {
                totalRequests = totalCount,
                todayRequests = todayCount,
                recentJokes = recentJokes.OrderByDescending(j => ((dynamic)j).timestamp).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve statistics");
            return new
            {
                totalRequests = 0,
                todayRequests = 0,
                recentJokes = new string[0],
                error = "Failed to retrieve statistics"
            };
        }
    }
}