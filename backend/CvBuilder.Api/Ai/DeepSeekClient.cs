using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CvBuilder.Api.Ai;

public class DeepSeekOptions
{
    /// <summary>Set via the DeepSeek__ApiKey environment variable. Never committed.</summary>
    public string ApiKey { get; set; } = "";

    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public string Model { get; set; } = "deepseek-chat";

    /// <summary>Low by default: this is a selection task, not a creative one.</summary>
    public double Temperature { get; set; } = 0.2;
}

/// <summary>Raised for anything the user should see rather than a stack trace.</summary>
public class DeepSeekException(string message) : Exception(message);

/// <summary>
/// Minimal DeepSeek chat client. The key stays server-side and never reaches the
/// browser, so the SPA talks only to our own API.
/// </summary>
public class DeepSeekClient(HttpClient http, DeepSeekOptions options)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey);
    public string Model => options.Model;

    /// <summary>Returns the assistant's message content, which the caller parses.</summary>
    public async Task<string> CompleteJsonAsync(string systemPrompt, string userMessage, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new DeepSeekException(
                "No DeepSeek API key configured. Set the DeepSeek__ApiKey environment variable and restart the API.");

        var request = new ChatRequest(
            options.Model,
            [new ChatMessage("system", systemPrompt), new ChatMessage("user", userMessage)],
            options.Temperature,
            new ResponseFormat("json_object"));

        using var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = content
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new DeepSeekException($"Could not reach DeepSeek: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            throw new DeepSeekException("DeepSeek did not respond in time.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                throw new DeepSeekException(
                    $"DeepSeek returned {(int)response.StatusCode} {response.StatusCode}: {Trim(body)}");

            var parsed = JsonSerializer.Deserialize<ChatResponse>(body, JsonOptions);
            var reply = parsed?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(reply))
                throw new DeepSeekException("DeepSeek returned an empty reply.");

            return reply;
        }
    }

    private static string Trim(string body) => body.Length <= 400 ? body : body[..400] + "…";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---- Wire format ------------------------------------------------------

    private record ChatRequest(
        string Model,
        ChatMessage[] Messages,
        double Temperature,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat);

    private record ChatMessage(string Role, string Content);

    private record ResponseFormat(string Type);

    private record ChatResponse(List<Choice>? Choices);

    private record Choice(ChatMessage? Message);
}
