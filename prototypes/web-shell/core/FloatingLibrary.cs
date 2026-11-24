using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Infrastructure;
using NexSpace.Windows;

internal sealed class FloatingLibrary
{
    readonly string database;
    readonly ContainerService containers;
    readonly WorkspaceService workspace;
    readonly ProfileService profiles;
    readonly WindowsContainerItemPlatformService shell = new();
    readonly ShellIcons icons = new();
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    FloatingLibrary(string db)
    {
        database = db;
        containers = new(new SqliteContainerRepository(db));
        Environment.SetEnvironmentVariable("NEXSPACE_DATA_DIR", Path.GetDirectoryName(db));
        workspace = new(new SqliteWorkspaceRepository(db), containers, new JsonEventLog());
        profiles = new(new SqliteProfileRepository(db));
    }

    public static void Start(string[] args, string root)
    {
        var isolated = Environment.GetEnvironmentVariable("NEXSPACE_FLOATING_TEST_DIR");
        if (!string.IsNullOrEmpty(isolated))
        {
            root = Path.GetFullPath(isolated);
            if (!root.StartsWith(Path.Combine(Path.GetTempPath(), "NexSpace-Phase4-"), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("El ensayo requiere un directorio temporal aislado.");
        }
        Directory.CreateDirectory(root);
        var db = Path.Combine(root, "floating.db");
        if (args[0] == "--prepare-floating")
        {
            if (File.Exists(db)) return;
            string pending = db + ".preparing-" + Guid.NewGuid().ToString("N");
            if (args.Length == 2)
            {
                using var src = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = args[1], Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                using var dst = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = pending, Pooling = false }.ToString());
                src.Open(); dst.Open(); src.BackupDatabase(dst);
            }
            var library = new FloatingLibrary(pending);
            _ = library.profiles.Load();
            if (args.Length == 1) library.Seed(root);
            File.Move(pending, db);
            return;
        }
        if (!File.Exists(db)) throw new IOException("Prepara primero la biblioteca de demostración.");
        int apartment = CoInitializeEx(0, 2);
        try { new FloatingLibrary(db).Serve(); }
        finally { if (apartment >= 0) CoUninitialize(); }
    }

    void Seed(string root)
    {
        var sources = Directory.CreateDirectory(Path.Combine(root, "recursos")).FullName;
        var open = new List<Guid>();
        foreach (var (name, place, names, glyph) in new[]{
            ("Archivos",new ContainerPlacement("primary",24,24,386,272),new[]{"Proyectos","Brief.txt","Notas.txt"},"\uE8B7"),
            ("Juegos",new ContainerPlacement("primary",24,316,386,272),new[]{"Partidas","Guías","Controles.txt"},"\uE7FC"),
            ("Aplicaciones",new ContainerPlacement("primary",1200,24,386,272),new[]{"Herramientas","Manuales","Inventario.txt"},"\uE71D")})
        {
            var group = Value(containers.Create(name, place));
            open.Add(group.Id);
            foreach (var label in names)
            {
                string path = Path.Combine(sources, label);
                if (Path.HasExtension(label)) { if (!File.Exists(path)) File.WriteAllText(path, "Recurso aislado de NexSpace."); }
                else Directory.CreateDirectory(path);
                Value(containers.AddReference(group.Id, Directory.Exists(path) ? ContainerItemKind.Folder : ContainerItemKind.File, path, label));
            }
            var category = Value(workspace.AddCategory(name, group.Id)).Categories.Last();
            Value(workspace.EditCategory(category.Id, name, glyph, group.Id));
        }
        Value(workspace.Change(w => w with { Session = w.Session with { OpenContainers = open } }));
    }

    static T Value<T>(ServiceResult<T> r) => r.Succeeded ? r.Value! : throw new Fault(r.Code);
    void Serve()
    {
        var session = Environment.GetEnvironmentVariable("NEXSPACE_PILOT_SESSION_ID");
        while (Console.ReadLine() is { } line)
        {
            long id = 0; object? data = null; string? error = null;
            Command? request = null;
            try
            {
                if (line.Length > 32768) throw new Fault("message-too-large");
                request = JsonSerializer.Deserialize<Command>(line, Json) ?? throw new Fault("invalid-message");
                id = request.Id;
                if (request.V != 2 || id < 1) throw new Fault("invalid-envelope");
                data = Execute(request);
            }
            catch (Fault ex) { error = ex.Message; }
            catch (JsonException) { error = "invalid-json"; }
            catch (ArgumentException) { error = "invalid-argument"; }
            catch (Exception) { error = "storage-or-platform-error"; }
            if (error != null) Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} {id} {error}");
            if (session is not null && request is { Op: "add" })
            {
                var source = request.Source is "picker" or "ole-drop" ? request.Source : "test-ipc";
                Console.Error.WriteLine(JsonSerializer.Serialize(new
                {
                    session, id, source, request.Profile, request.Target, ok = error is null, error
                }, Json));
            }
            Console.WriteLine(JsonSerializer.Serialize(new { v = 2, id, data, error }, Json));
        }
    }

    object Manifest(ProfileState ps)
    {
        var w = workspace.Load();
        return new
        {
            profile = ps.ActiveId,
            ps.Profiles,
            vertical = w.Dock.Orientation == DockOrientation.Vertical || (w.Dock.Orientation == DockOrientation.Automatic && w.Dock.Edge is DockEdge.Left or DockEdge.Right),
            groups = containers.GetAll().Select(c => new { c.Id, c.Name, c.Placement, c.IsCollapsed, c.IsPositionLocked, shown = w.Session.OpenContainers.Contains(c.Id) && !hiddenAtStartup.Contains(c.Id) }),
            categories = w.Categories.Select(c => new { c.Id, c.Name, c.Glyph, c.ContainerId }),
            dockMonitor = w.Dock.MonitorKey,
            w.Session.RestoreContainers
        };
    }

    readonly HashSet<Guid> hiddenAtStartup = [];
    void RestoreSession(bool starting)
    {
        var state = profiles.Load();
        if (starting && !state.RestoreLast && state.ActiveId != state.DefaultId) profiles.Switch(state.DefaultId);
        hiddenAtStartup.Clear();
        var session = workspace.Load().Session;
        if (!session.RestoreContainers) hiddenAtStartup.UnionWith(session.OpenContainers);
    }

    object Execute(Command r)
    {
        var ps = profiles.Load();
        if (r.Op == "startup") { RestoreSession(true); return true; }
        if (r.Op == "manifest") return Manifest(ps);
        if (r.Op == "diagnostics") return new
        {
            database,
            profile = ps.ActiveId,
            profileName = ps.Profiles.FirstOrDefault(p => p.Id == ps.ActiveId)?.Name,
            corePid = Environment.ProcessId,
            session = Environment.GetEnvironmentVariable("NEXSPACE_PILOT_SESSION_ID")
        };
        if (r.Profile != ps.ActiveId) throw new Fault("stale-profile");
        var target = r.Target.GetValueOrDefault();
        var item = r.Item.GetValueOrDefault();
        string Name() => r.Name ?? throw new Fault("invalid-name");
        NexContainer Group() => containers.Get(target) ?? throw new Fault("not-found");
        DockCategory Category() => workspace.Load().Categories.FirstOrDefault(c => c.Id == target) ?? throw new Fault("not-found");
        void Visible(Guid id, bool visible) => Value(workspace.Change(w => w with
        {
            Session = w.Session with
            {
                OpenContainers = (visible ? w.Session.OpenContainers.Append(id).Distinct() : w.Session.OpenContainers.Where(i => i != id)).ToArray()
            }
        }));
        switch (r.Op)
        {
            case "createGroup": var added = Value(containers.Create(Name())); Visible(added.Id, true); return added.Id;
            case "renameGroup": Value(containers.Rename(target, Name())); break;
            case "deleteGroup":
                _ = Group();
                Value(workspace.Change(w => w with
                {
                    Session = w.Session with { OpenContainers = w.Session.OpenContainers.Where(id => id != target).ToArray() },
                    Categories = w.Categories.Select(c => c.ContainerId == target ? c with { ContainerId = null } : c).ToArray()
                }));
                Value(containers.Delete(target)); break;
            case "show": _ = Group(); Visible(target, r.Flag ?? true); hiddenAtStartup.Remove(target); break;
            case "lock": Value(containers.UpdatePresentation(target, positionLocked: r.Flag)); break;
            case "collapse": Value(containers.UpdatePresentation(target, collapsed: r.Flag)); break;
            case "listView": Value(containers.UpdatePresentation(target, viewMode: r.Flag == true ? ContainerViewMode.List : ContainerViewMode.Grid)); break;
            case "placement": Value(containers.SavePlacement(target, r.Placement ?? throw new Fault("invalid-placement"), recoverPosition: r.Flag == true)); break;
            case "createCategory": Value(workspace.AddCategory(Name(), r.Link)); break;
            case "editCategory": var c = Category(); Value(workspace.EditCategory(target, Name(), c.Glyph, r.Link)); break;
            case "deleteCategory": Value(workspace.RemoveCategory(target)); break;
            case "moveCategory": Value(workspace.MoveCategory(target, r.Offset)); break;
            case "vertical": Value(workspace.Change(w => w with { Dock = w.Dock with { Orientation = r.Flag == true ? DockOrientation.Vertical : DockOrientation.Horizontal, Edge = r.Flag == true ? DockEdge.Left : DockEdge.Bottom } })); break;
            case "createProfile": return profiles.Create(Name(), r.Flag == true).Id;
            case "renameProfile": profiles.Rename(target, Name()); break;
            case "switchProfile": profiles.Switch(target); RestoreSession(false); break;
            default:
                var cat = r.Category ? Category() : null;
                var group = cat == null ? Group() : cat.ContainerId is Guid link ? containers.Get(link) : null;
                if (cat?.ContainerId != null && group == null) throw new Fault("not-found");
                var items = cat == null ? group!.Items : workspace.Resolve(cat);
                if (r.Op == "page")
                {
                    var entries = items.Skip(Math.Clamp(r.Offset, 0, items.Count)).Take(24).ToArray();
                    var art = new Dictionary<string, string?>();
                    var rows = entries.Select(i =>
                    {
                        var status = shell.Inspect(i);
                        var (key, png) = status.Exists ? icons.Get(i, Math.Clamp(r.IconPixels ?? 48, 48, 96)) : ("", null);
                        if (png is not null) art.TryAdd(key, png);
                        return new { i.Id, name = i.DisplayName, status = status.Code, kind = i.Kind.ToString(), key };
                    }).ToArray();
                    var dock = cat == null ? null : workspace.Load().Dock;
                    return new { vertical = dock != null && (dock.Orientation == DockOrientation.Vertical || (dock.Orientation == DockOrientation.Automatic && dock.Edge is DockEdge.Left or DockEdge.Right)), name = cat?.Name ?? group!.Name, rows, art, total = items.Count, list = group?.ViewMode == ContainerViewMode.List, locked = group?.IsPositionLocked, collapsed = group?.IsCollapsed };
                }
                if (r.Op == "add")
                {
                    var path = r.Path ?? throw new Fault("invalid-target");
                    if (!Path.IsPathFullyQualified(path)) throw new Fault("invalid-target");
                    var folder = Directory.Exists(path);
                    if (!folder && !File.Exists(path)) throw new Fault("missing");
                    var label = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
                    var entry = new ReferenceDropEntry(path, label, folder);
                    if (group != null)
                    {
                        var result = new ReferenceDropProcessor(containers).Process(group.Id, [entry]);
                        if (result.Added != 1) throw new Fault(result.Errors.First());
                    }
                    else Value(workspace.AddReference(target, ReferenceDropProcessor.Kind(entry), path, label));
                    break;
                }
                var reference = items.FirstOrDefault(i => i.Id == item) ?? throw new Fault("unknown-reference");
                switch (r.Op)
                {
                    case "open": var opened = shell.Open(reference); if (!opened.Succeeded) throw new Fault(opened.Code); break;
                    case "remove": if (group != null) Value(containers.RemoveReference(group.Id, item)); else Value(workspace.RemoveReference(target, item)); break;
                    case "rename": if (group != null) Value(containers.UpdateItemReference(group.Id, reference with { DisplayName = Name() })); else Value(workspace.RenameReference(target, item, Name())); break;
                    case "moveItem": if (group != null) Value(containers.MoveReference(group.Id, item, r.Offset)); else Value(workspace.MoveReference(target, item, r.Offset)); break;
                    default: throw new Fault("unsupported-operation");
                }
                break;
        }
        return true;
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();

    sealed record Command(int V, long Id, string Op, Guid? Profile, Guid? Target, Guid? Item, bool Category, Guid? Link, string? Name, bool? Flag, int Offset, string? Path, ContainerPlacement? Placement, int? IconPixels, string? Source);
}
