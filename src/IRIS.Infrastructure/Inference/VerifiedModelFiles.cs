using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Infrastructure.Native;

namespace IRIS.Infrastructure.Inference;

public sealed class VerifiedModelFiles : IDisposable
{
    public const string RuntimeSha256 = "6e3a253f45117d1aec2adb2de0699ded0f6dccb683a798802d9e0dabae291f2c";
    public const string ModelSha256 = "3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597";
    public const string ModelName = "Qwen3-4B-Instruct-2507-Q4_K_M.gguf";
    public string RuntimeDirectory { get; }
    public string ModelPath { get; }
    public string ServerPath => Path.Combine(RuntimeDirectory, "llama-server.exe");
    private readonly VerifiedFileSet verified;

    private VerifiedModelFiles(string root, VerifiedFileSet verified)
    {
        RuntimeDirectory = Path.Combine(root, "runtimes", "llama-b11901");
        ModelPath = Path.Combine(root, "models", ModelName);
        this.verified = verified;
    }

    public static async Task<VerifiedModelFiles> LoadAsync(string root, CancellationToken cancellationToken)
    {
        root = Path.GetFullPath(root);
        var package = new TrustedModelPackage("llama-b11901-bin-win-vulkan-x64.zip", RuntimeSha256, "llama-b11901", ModelName, ModelSha256, 2497281120);
        return new VerifiedModelFiles(root, await VerifiedFileSet.LoadAsync(root, package, cancellationToken).ConfigureAwait(false));
    }

    public void Dispose() => verified.Dispose();
}
