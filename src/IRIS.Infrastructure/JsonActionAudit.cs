using System;
using System.IO;
using System.Text.Json;
using IRIS.Core.Contracts;

namespace IRIS.Infrastructure;

public sealed class JsonActionAudit : IActionAudit
{
    private readonly string path;
    private readonly object gate = new();
    public JsonActionAudit(string directory) => path = Path.Combine(directory, "actions.jsonl");
    public void Record(ActionAuditEvent entry)
    {
        lock (gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Bounded technical log, one previous generation, no conversation payload.
            if (File.Exists(path) && new FileInfo(path).Length > 1048576) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
    }
}
