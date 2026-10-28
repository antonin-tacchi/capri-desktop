using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IRIS.Infrastructure.Inference;

namespace IRIS.Infrastructure;

// Application resources are separate from private settings/memories. A portable
// installation must not rely on a developer's LocalAppData cache being present.
public static class EngineAssets
{
    public static readonly string[] ChatFiles = {
        "packages/llama-b11901-bin-win-vulkan-x64.zip", "models/" + VerifiedModelFiles.ModelName,
        "runtimes/llama-b11901/llama-server.exe"
    };
    public static readonly string[] SpeechFiles = {
        "packages/whisper-b5454-x64.zip", "models/ggml-small.bin", "runtimes/whisper-b5454/Release/whisper-cli.exe"
    };
    public static string Resolve(string applicationDirectory, string userDirectory)
    {
        string application = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDirectory));
        string[] candidates = {
            Path.Combine(application, "engine-data"),
            Path.Combine(Directory.GetParent(application)?.FullName ?? application, "IRIS-engine-data"),
            Path.GetFullPath(userDirectory)
        };
        return candidates.FirstOrDefault(root => ChatFiles.Concat(SpeechFiles).All(file => File.Exists(Path.Combine(root, file))))
            ?? candidates.FirstOrDefault(root => ChatFiles.All(file => File.Exists(Path.Combine(root, file))))
            ?? Path.GetFullPath(userDirectory);
    }
    public static IReadOnlyList<string> Missing(string root, bool speech) => (speech ? SpeechFiles : ChatFiles)
        .Select(file => Path.Combine(root, file)).Where(file => !File.Exists(file)).ToArray();
}
