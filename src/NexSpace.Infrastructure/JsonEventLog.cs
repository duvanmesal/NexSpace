using System.Text.Json;
using NexSpace.Application;

namespace NexSpace.Infrastructure;

public sealed class JsonEventLog : IEventLog
{
    private readonly string _path;
    private readonly object _gate = new();

    public JsonEventLog()
    {
        var dataDirectory = Environment.GetEnvironmentVariable("NEXSPACE_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDirectory)) dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexSpace", "data");
        var fullDataDirectory = Path.GetFullPath(dataDirectory);
        var applicationDirectory = Directory.GetParent(fullDataDirectory)?.FullName ?? fullDataDirectory;
        _path = Path.Combine(applicationDirectory, "logs", "events.jsonl");
    }

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
