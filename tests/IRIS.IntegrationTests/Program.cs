using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IRIS.AI.Conversation;
using IRIS.Core.Services;
using IRIS.Infrastructure;
using IRIS.Infrastructure.Inference;
using IRIS.Memory;
using IRIS.Core.Contracts;

namespace IRIS.IntegrationTests;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--local-memory") return await CheckRealMemoryAsync();
        if (args.Length == 2 && args[0] == "--job-worker")
        {
            using var emergencyWorker = new EmergencyStopService();
            await using var worker = new LocalInferenceService(LocalSettingsStore.DefaultDirectory, emergencyWorker);
            await worker.StartAsync(false, default);
            await File.WriteAllTextAsync(args[1], worker.ProcessId!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await Task.Delay(Timeout.Infinite);
            return 0;
        }
        if (args.Length != 1 || args[0] != "--local-model")
        {
            Console.WriteLine("Aucun modèle lancé. Utilisez --local-model après acquisition explicitement autorisée des fichiers.");
            return 0;
        }
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            using var emergency = new EmergencyStopService();
            await using var inference = new LocalInferenceService(LocalSettingsStore.DefaultDirectory, emergency);
            var timer = Stopwatch.StartNew();
            await inference.StartAsync(false, timeout.Token);
            double startupSeconds = timer.Elapsed.TotalSeconds;
            using Process process = Process.GetProcessById(inference.ProcessId ?? throw new InvalidOperationException("Processus manquant"));
            var conversation = new ConversationService(inference);
            timer.Restart();
            string answer = await conversation.SendAsync("Présente-toi en français en deux phrases, puis indique clairement si tu peux contrôler mon ordinateur actuellement.", new(), timeout.Token);
            double responseSeconds = timer.Elapsed.TotalSeconds;
            process.Refresh();
            long workingSet = process.WorkingSet64;
            if (string.IsNullOrWhiteSpace(answer)) throw new InvalidDataException("Réponse vide");
            Console.WriteLine("PASS chargement avec fichiers verrouillés et empreintes vérifiées");
            Console.WriteLine("PASS écoute PID vérifiée sur loopback et génération authentifiée");
            Console.WriteLine("Réponse réelle : " + answer);
            emergency.Stop();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Console.WriteLine("PASS arrêt d’urgence : processus d’inférence terminé");
            string output = Path.GetFullPath("out/local-model-smoke.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                at = DateTimeOffset.UtcNow, model = VerifiedModelFiles.ModelName, runtime = "llama.cpp b11901 Vulkan",
                startupSeconds, responseSeconds, workingSetBytes = workingSet, answer,
                note = "Un échantillon uniquement ; ni évaluation complète du français ni mesure de tokens par seconde."
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Chargement {startupSeconds:F2} s ; réponse {responseSeconds:F2} s ; RAM processus {workingSet / 1048576.0:F0} Mio.");
            await CheckOwnerCrashAsync();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static async Task<int> CheckRealMemoryAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "IRIS-real-memory-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            using var emergency = new EmergencyStopService();
            await using var inference = new LocalInferenceService(LocalSettingsStore.DefaultDirectory, emergency);
            var memory = new MemoryService(directory);
            await memory.SetPolicyAsync(new(true, true), timeout.Token);
            MemoryItem item = await memory.AddAsync("Couleur de test", "Ma couleur de test préférée porte le nom inventé azur-iris-742.", MemoryKind.Preference, timeout.Token);
            var reopened = new MemoryService(directory);
            var conversation = new ConversationService(inference, reopened);
            await inference.StartAsync(false, timeout.Token);
            string remembered = await conversation.SendAsync("Quelle est ma couleur de test préférée ? Réponds uniquement avec le nom exact enregistré.", new(0), timeout.Token);
            if (!remembered.Contains("azur-iris-742", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Le modèle n’a pas utilisé le souvenir de test : " + remembered);
            Console.WriteLine("PASS modèle réel : souvenir relu depuis SQLite après réouverture : " + remembered);
            await memory.DeleteAsync(item.Id, item.Revision, timeout.Token);
            string forgotten = await conversation.SendAsync("Quelle est ma couleur de test préférée ? Si elle n’est pas enregistrée, dis que tu ne sais pas.", new(0), timeout.Token);
            if (forgotten.Contains("azur-iris-742", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Le modèle réutilise le souvenir supprimé.");
            Console.WriteLine("PASS modèle réel : souvenir supprimé et contexte précédent invalidé : " + forgotten);
            emergency.Stop();
            await inference.StopAsync();
            string output = Path.GetFullPath("out/local-memory-smoke.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, remembered, forgotten, sqlite = MemoryService.SqliteVersion,
                note = "Donnée synthétique, base temporaire supprimée ; deux échantillons ne prouvent pas une robustesse générale." }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static async Task CheckOwnerCrashAsync()
    {
        string marker = Path.Combine(Path.GetTempPath(), "IRIS-job-test-" + Guid.NewGuid().ToString("N") + ".txt");
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--job-worker"); start.ArgumentList.Add(marker);
        using Process owner = Process.Start(start) ?? throw new IOException("Worker de test non démarré");
        Process? engine = null;
        try
        {
            DateTime until = DateTime.UtcNow.AddSeconds(130);
            while (!File.Exists(marker))
            {
                if (owner.HasExited || DateTime.UtcNow >= until) throw new IOException("Worker de test indisponible");
                await Task.Delay(50);
            }
            // Wait for the writer to close its one-line marker.
            string text = "";
            while (text.Length == 0)
            {
                try { text = await File.ReadAllTextAsync(marker); } catch (IOException) { }
                if (DateTime.UtcNow >= until) throw new TimeoutException();
                if (text.Length == 0) await Task.Delay(20);
            }
            engine = Process.GetProcessById(int.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
            engine.EnableRaisingEvents = true;
            owner.Kill(false); // Kill only our worker, so cleanup must come from the Job Object.
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await engine.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Console.WriteLine("PASS arrêt brutal du propriétaire : le Job Object termine le moteur enfant");
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(false); await owner.WaitForExitAsync(); }
            if (engine is { HasExited: false }) { engine.Kill(true); await engine.WaitForExitAsync(); }
            engine?.Dispose();
            if (File.Exists(marker)) File.Delete(marker);
        }
    }
}
