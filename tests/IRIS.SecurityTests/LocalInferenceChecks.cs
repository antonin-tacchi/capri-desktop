using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IRIS.AI.Conversation;
using IRIS.AI.Inference;

namespace IRIS.SecurityTests;

public static class LocalInferenceChecks
{
    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond = (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Bonjour.\"}}]}") });
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Respond(request, cancellationToken); }
    }
    private sealed class Provider : ITextGenerationProvider
    {
        public IReadOnlyList<ChatEntry> Last = Array.Empty<ChatEntry>();
        public bool Fail;
        public Task<string> GenerateAsync(IReadOnlyList<ChatEntry> messages, GenerationSettings settings, CancellationToken cancellationToken)
        {
            Last = messages;
            if (Fail) throw new IOException("Synthetic engine failure");
            return Task.FromResult("Une réponse de test.");
        }
    }
    private static readonly ChatEntry[] Messages = { new("system", "Tu es IRIS."), new("user", "Bonjour.") };
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static async Task<string[]> RunAsync()
    {
        var passed = new List<string>();
        foreach (string url in new[] { "https://127.0.0.1:9000/", "http://localhost:9000/", "http://0.0.0.0:9000/", "http://192.168.1.2:9000/", "http://127.0.0.1:80/", "http://127.0.0.1:9000/other", "http://a@127.0.0.1:9000/", "http://127.0.0.1:9000/?redirect=1" })
        {
            bool refused = false;
            try { using var client = new LocalCompletionClient(new Uri(url), new string('a', 64)); }
            catch (ArgumentException) { refused = true; }
            Require(refused, "Unsafe destination allowed: " + url);
        }
        passed.Add("PASS only dedicated literal loopback endpoints are accepted");
        using (var handler = new Handler())
        using (var client = new LocalCompletionClient(new Uri("http://127.0.0.1:9000/"), new string('a', 64), handler))
        {
            handler.Respond = async (request, token) =>
            {
                Require(request.RequestUri!.AbsoluteUri == "http://127.0.0.1:9000/v1/chat/completions", "Unexpected route");
                Require(request.Headers.Authorization?.Scheme == "Bearer", "Authentication missing");
                string body = await request.Content!.ReadAsStringAsync(token);
                Require(!body.Contains("tools", StringComparison.Ordinal) && body.Contains("system", StringComparison.Ordinal), "Tool exposure or system prompt missing");
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Bonjour.\"}}]}") };
            };
            Require(await client.GenerateAsync(Messages, new(), default) == "Bonjour.", "Response not parsed");
            handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://example.com/") } });
            bool refused = false;
            try { await client.GenerateAsync(Messages, new(), default); } catch (IOException) { refused = true; }
            Require(refused && handler.Calls == 2, "Redirect followed");
        }
        passed.Add("PASS local requests authenticate, omit tools and reject redirects");
        foreach (string invalid in new[] {
            "{\"choices\":[{\"message\":{\"content\":\"\",\"tool_calls\":[{}]}}]}",
            "{\"choices\":[{\"message\":{\"content\":\"\"}}]}", new string('x', 65537), "invalid JSON" })
        {
            using var handler = new Handler { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(invalid, Encoding.UTF8) }) };
            using var client = new LocalCompletionClient(new Uri("http://127.0.0.1:9000/"), new string('b', 64), handler);
            bool refused = false;
            try { await client.GenerateAsync(Messages, new(), default); }
            catch (Exception error) when (error is InvalidDataException or JsonException) { refused = true; }
            Require(refused, "Untrusted model response accepted");
        }
        passed.Add("PASS tool calls, empty, oversized and malformed model responses are rejected");
        using (var handler = new Handler { Respond = async (_, token) => { await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); } })
        using (var client = new LocalCompletionClient(new Uri("http://127.0.0.1:9000/"), new string('c', 64), handler))
        {
            using var cancel = new CancellationTokenSource();
            Task<string> pending = client.GenerateAsync(Messages, new(), cancel.Token); cancel.Cancel();
            bool cancelled = false;
            try { await pending.WaitAsync(TimeSpan.FromSeconds(2)); } catch (OperationCanceledException) { cancelled = true; }
            Require(cancelled, "Generation ignored cancellation");
        }
        passed.Add("PASS cancelling a generation interrupts the HTTP request");
        var provider = new Provider();
        var conversation = new ConversationService(provider);
        await conversation.SendAsync("Mon pseudonyme est Test.", new(), default);
        await conversation.SendAsync("Tu te souviens ?", new(0), default);
        Require(provider.Last.Count == 4 && provider.Last[0].Role == "system" && provider.Last[0].Content.Contains("sans sarcasme", StringComparison.Ordinal), "Session context or personality lost");
        await conversation.ClearAsync();
        provider.Fail = true;
        try { await conversation.SendAsync("Ne conserve pas cet échec.", new(), default); } catch (IOException) { }
        provider.Fail = false;
        await conversation.SendAsync("Nouveau départ.", new(), default);
        Require(provider.Last.Count == 2 && provider.Last[1].Content == "Nouveau départ.", "Cleared or failed message retained");
        passed.Add("PASS conversation retains only successful session context and supports clearing");
        for (int index = 0; index < 20; index++) await conversation.SendAsync(new string('x', 2000), new(), default);
        Require(provider.Last.Count <= 12 && provider.Last.Sum(m => m.Content.Length) < 20000, "Conversation context unbounded");
        passed.Add("PASS long conversations keep a bounded context");
        return passed.ToArray();
    }
}
