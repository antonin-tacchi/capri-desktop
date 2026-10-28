using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IRIS.AI.Inference;

public sealed record ChatEntry(string Role, string Content);
public sealed record GenerationSettings(double Temperature = 0.6, int MaxTokens = 512);

public interface ITextGenerationProvider
{
    Task<string> GenerateAsync(IReadOnlyList<ChatEntry> messages, GenerationSettings settings, CancellationToken cancellationToken);
}

public interface ILocalInferenceService : ITextGenerationProvider
{
    bool IsReady { get; }
    Task StartAsync(bool cpuOnly, CancellationToken cancellationToken);
    Task StopAsync();
}

public sealed class LocalCompletionClient : ITextGenerationProvider, IDisposable
{
    private readonly HttpClient client;

    public LocalCompletionClient(Uri endpoint, string apiKey, HttpMessageHandler? testHandler = null)
    {
        if (endpoint.Scheme != "http" || endpoint.Host != "127.0.0.1" || endpoint.Port < 1024
            || endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException("Seule une destination HTTP IPv4 loopback dédiée est autorisée.");
        if (apiKey.Length != 64 || apiKey.Any(character => !Uri.IsHexDigit(character))) throw new ArgumentException("Clé locale invalide.");
        client = new HttpClient(testHandler ?? new SocketsHttpHandler
        {
            UseProxy = false, AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(2)
        }) { BaseAddress = endpoint, Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        using var response = await client.GetAsync("health", HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<string> GenerateAsync(IReadOnlyList<ChatEntry> messages, GenerationSettings settings, CancellationToken cancellationToken)
    {
        if (messages.Count is < 2 or > 32 || messages[0].Role != "system" || messages.Skip(1).Any(m => m.Role is not ("user" or "assistant"))
            || messages.Any(m => string.IsNullOrWhiteSpace(m.Content)) || messages.Sum(m => m.Content.Length) > 20000
            || !double.IsFinite(settings.Temperature) || settings.Temperature is < 0 or > 1 || settings.MaxTokens is < 1 or > 1024)
            throw new ArgumentException("Contexte ou paramètres de génération invalides.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = "iris-local", messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                temperature = settings.Temperature, max_tokens = settings.MaxTokens, stream = false
            })
        };
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new IOException("Le moteur local a refusé la génération (HTTP " + (int)response.StatusCode + ").");
        const int limit = 65536;
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Réponse locale trop volumineuse.");
        await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + count > limit) throw new InvalidDataException("Réponse locale trop volumineuse.");
            buffer.Write(chunk, 0, count);
        }
        using JsonDocument body = JsonDocument.Parse(buffer.ToArray());
        JsonElement message = body.RootElement.GetProperty("choices")[0].GetProperty("message");
        if (message.TryGetProperty("tool_calls", out JsonElement calls) && calls.ValueKind != JsonValueKind.Null && calls.GetArrayLength() > 0)
            throw new InvalidDataException("Les demandes d’outils du modèle sont désactivées.");
        string? answer = message.GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(answer) || answer.Length > 16000) throw new InvalidDataException("Réponse du modèle vide ou invalide.");
        return answer;
    }

    public void Dispose() => client.Dispose();
}
