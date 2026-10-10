using System.Text.Json;
using NexSpace.Application;

namespace NexSpace.Infrastructure;

public sealed class JsonEventLog : IEventLog
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexSpace", "logs", "events.jsonl");
    private readonly object _gate = new();
    public void Write(string eventName, string detail, string? error = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var row = JsonSerializer.Serialize(new { timestampUtc = DateTimeOffset.UtcNow, eventName, detail, errorCode = error });
            lock (_gate) File.AppendAllText(_path, row + Environment.NewLine);
        }
        catch { /* Diagnostics must never prevent startup or shutdown. */ }
    }
}
