using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Infrastructure;
using NexSpace.Windows;

Console.InputEncoding = Console.OutputEncoding = new UTF8Encoding(false);
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "pilot-data"));
if (args is ["--floating-beta", var betaRoot])
{
    try { FloatingLibrary.StartBeta(betaRoot); }
    catch (Exception error)
    {
        Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} beta-storage {error.GetType().Name}");
        Environment.ExitCode = 1;
    }
    return;
}
if(args.FirstOrDefault() is "--floating" or "--prepare-floating"){ FloatingLibrary.Start(args,root); return; }
var db = Path.Combine(root, "library.db");
var marker = Path.Combine(root, "container.id");
var service = new ContainerService(new SqliteContainerRepository(db));
var platform = new WindowsContainerItemPlatformService();
string[] labels = ["Documentos", "Proyectos", "Archivo", "Imágenes", "Música", "Notas ausentes"];
string Fixture(int index) => Path.Combine(root, $"ficha-{index}.txt");

if (args is ["--prepare"] or ["--prepare", _])
{
    Directory.CreateDirectory(root);
    if (File.Exists(marker)) return;
    if (File.Exists(db)) throw new IOException("La preparación anterior quedó incompleta. Conservar esa copia y usar otro directorio de salida.");
    if (args.Length == 2)
    {
        using var original = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = args[1], Mode = SqliteOpenMode.ReadOnly }.ToString());
        using var copy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db }.ToString());
        original.Open();
        copy.Open();
        original.BackupDatabase(copy);
    }
    _ = new SqliteProfileRepository(db).Load();
    var group = service.Create("Archivos · piloto", new("primary", 24, 24, 386, 272)).Value!;
    for (int i = 0; i < labels.Length; i++)
    {
        if (i < labels.Length - 1) File.WriteAllText(Fixture(i), $"Ficha aislada de NexSpace. {labels[i]}");
        if (!service.AddReference(group.Id, ContainerItemKind.File, Fixture(i), labels[i]).Succeeded)
            throw new IOException("No se pudo preparar la referencia.");
    }
    File.WriteAllText(marker, group.Id.ToString());
    return;
}

if (args.Length != 0 || !File.Exists(db) || !Guid.TryParse(File.ReadAllText(marker), out var containerId)) return;
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
var icons = new Dictionary<string, string?>();
string? IconFor(string path)
{
    var key = Path.GetExtension(path);
    if (!icons.TryGetValue(key, out var icon))
    {
        using var shell = Icon.ExtractAssociatedIcon(path);
        using var bitmap = shell?.ToBitmap();
        using var buffer = new MemoryStream();
        bitmap?.Save(buffer, ImageFormat.Png);
        icon = buffer.Length is > 0 and <= 32768 ? "data:image/png;base64," + Convert.ToBase64String(buffer.ToArray()) : null;
        icons[key] = icon;
    }
    return icon;
}
bool Allowed(ContainerItemReference item) => item.Kind == ContainerItemKind.File &&
    Enumerable.Range(0, labels.Length).Any(i => StringComparer.OrdinalIgnoreCase.Equals(item.Target, Fixture(i)));
object Run(Request r)
{
    var group = service.Get(containerId) ?? throw new Fault("not-found");
    var item = group.Items.FirstOrDefault(x => x.Id == r.Item);
    switch (r.Op)
    {
        case "list": return service.GetAll().Select(c => new { c.Id, c.Name }).ToArray();
        case "load":
            return new
            {
                group.Id,
                group.IsCollapsed,
                group.Placement,
                items = group.Items.Select(i =>
                {
                    var exists = Allowed(i) && platform.Inspect(i).Exists;
                    return new { i.Id, name = i.DisplayName, exists, icon = exists ? IconFor(i.Target) : null };
                }).ToArray()
            };
        case "placement" when r.X is >= -65536 and <= 65536 && r.Y is >= -65536 and <= 65536 &&
             r.Width is >= 320 and <= 880 && r.Height is >= 220 and <= 680:
            Check(service.SavePlacement(group.Id, new("primary", r.X.Value, r.Y.Value, r.Width.Value, r.Height.Value))); break;
        case "collapse" when r.Collapsed.HasValue: Check(service.UpdatePresentation(group.Id, collapsed: r.Collapsed)); break;
        case "open" or "rename" or "reorder":
            if (item is null || !Allowed(item)) throw new Fault("unknown-reference");
            if (r.Op == "open")
            {
                var result = platform.Open(item);
                if (!result.Succeeded) throw new Fault(result.Code);
            }
            else if (r.Op == "rename" && r.Name is { Length: > 0 and <= 260 })
                Check(service.UpdateItemReference(group.Id, item with { DisplayName = r.Name }));
            else if (r.Op == "reorder" && r.Offset is -1 or 1) Check(service.MoveReference(group.Id, item.Id, r.Offset.Value));
            else throw new Fault("invalid-request");
            break;
        default: throw new Fault("unsupported-operation");
    }
    return true;
}

while (Console.ReadLine() is { } line)
{
    long id = 0;
    object? data = null;
    string? error = null;
    try
    {
        if (line.Length > 2048) throw new Fault("message-too-large");
        var request = JsonSerializer.Deserialize<Request>(line, options) ?? throw new Fault("invalid-request");
        id = request.Id;
        if (id < 1 || request.V != 1) throw new Fault("invalid-envelope");
        data = Run(request);
    }
    catch (Fault f) { error = f.Message; }
    catch (JsonException) { error = "invalid-json"; }
    catch (Exception) { error = "storage-or-platform-error"; }
    if (error != null) Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} request={id} error={error}");
    Console.WriteLine(JsonSerializer.Serialize(new { v = 1, id, data, error }, options));
}
static void Check<T>(ServiceResult<T> result) { if (!result.Succeeded) throw new Fault(result.Code); }
sealed class Fault(string code) : Exception(code);
sealed record Request(int V, long Id, string Op, Guid? Item, string? Name, int? Offset, int? X, int? Y, int? Width, int? Height, bool? Collapsed);
