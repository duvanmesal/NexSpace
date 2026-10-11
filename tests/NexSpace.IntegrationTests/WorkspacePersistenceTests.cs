using Microsoft.Data.Sqlite;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Infrastructure;
using NexSpace.Presentation;
using NexSpace.Windows;
using Xunit;

namespace NexSpace.IntegrationTests;

public sealed class WorkspacePersistenceTests
{
    private sealed class Log : IEventLog { public void Write(string name, string detail, string? error = null) { } }
    private sealed class FailingLog : IEventLog { public void Write(string name, string detail, string? error = null) => throw new IOException("Isolated diagnostic failure"); }
    [Fact]
    public void Observer_and_diagnostic_failures_do_not_lose_committed_metadata()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path, "test.db");
        var service = new WorkspaceService(new SqliteWorkspaceRepository(db), new(new SqliteContainerRepository(db)), new FailingLog());
        var notified = 0;
        service.Changed += () => throw new InvalidOperationException("Isolated observer failure");
        service.Changed += () => notified++;
        Assert.True(service.AddCategory("Survives").Succeeded);
        Assert.Equal(1, notified);
        Assert.Equal("Survives", new SqliteWorkspaceRepository(db).Load().Categories.Single().Name);
    }
    [Fact]
    public void Categories_reorder_rename_remove_and_repeated_reopens_preserve_originals()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path, "test.db");
        var path = Path.Combine(scope.Path, "original.txt"); File.WriteAllText(path, "Immutable original");
        var cs = new ContainerService(new SqliteContainerRepository(db));
        var service = new WorkspaceService(new SqliteWorkspaceRepository(db), cs, new Log());
        Assert.True(service.AddCategory("Files").Succeeded); Assert.True(service.AddCategory("Apps").Succeeded);
        var c = service.Load().Categories[0];
        Assert.True(service.AddReference(c.Id, ContainerItemKind.File, path, "Original").Succeeded);
        Assert.False(service.AddReference(c.Id, ContainerItemKind.File, path, "Duplicate").Succeeded);
        Assert.True(service.AddReference(c.Id, ContainerItemKind.SystemResource, "shell:AppsFolder", "Apps").Succeeded);
        var id = service.Load().Categories[0].Items[0].Id;
        Assert.True(service.RenameReference(c.Id, id, "Visible name").Succeeded);
        Assert.True(service.MoveReference(c.Id, id, 1).Succeeded);
        Assert.True(service.MoveCategory(c.Id, 1).Succeeded);
        Assert.True(service.Change(w => w with { Dock = w.Dock with { Edge = DockEdge.Left, ItemSize = 56, AccentKey = "copper" } }).Succeeded);
        for (var index = 0; index < 12; index++)
        {
            var loaded = new SqliteWorkspaceRepository(db).Load();
            Assert.Equal(c.Id, loaded.Categories[1].Id); Assert.Equal("Visible name", loaded.Categories[1].Items[1].DisplayName);
            Assert.Equal(DockEdge.Left, loaded.Dock.Edge); Assert.Equal(56, loaded.Dock.ItemSize);
        }
        Assert.True(service.RemoveReference(c.Id, id).Succeeded); Assert.True(service.RemoveCategory(c.Id).Succeeded);
        Assert.Equal("Immutable original", File.ReadAllText(path)); using (File.Open(db, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }
    [Fact]
    public void Linked_category_reads_current_container_without_cloning_or_deleting_it()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path, "test.db");
        var cs = new ContainerService(new SqliteContainerRepository(db)); var c = cs.Create("Work").Value!;
        var service = new WorkspaceService(new SqliteWorkspaceRepository(db), cs, new Log());
        Assert.True(service.AddCategory("Live", c.Id).Succeeded); var category = service.Load().Categories[0];
        Assert.Empty(category.Items); Assert.Empty(service.Resolve(category));
        Assert.True(cs.AddReference(c.Id, ContainerItemKind.SystemResource, "shell:AppsFolder", "Apps").Succeeded);
        Assert.Single(service.Resolve(category));
        Assert.False(service.AddReference(category.Id, ContainerItemKind.SystemResource, "shell:Downloads", "Downloads").Succeeded);
        Assert.True(service.RemoveCategory(category.Id).Succeeded); Assert.Single(cs.Get(c.Id)!.Items);
        Assert.True(service.AddCategory("Live", c.Id).Succeeded); category = service.Load().Categories[0];
        Assert.True(cs.Delete(c.Id).Succeeded); Assert.Empty(service.Resolve(category));
    }
    [Fact]
    public void Stale_workspace_save_is_rejected_and_corrupt_document_not_reset()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path, "test.db");
        var repo = new SqliteWorkspaceRepository(db); var first = repo.Load(); repo.Save(first);
        Assert.Throws<InvalidOperationException>(() => repo.Save(first));
        using (var connection = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "UPDATE workspace_state SET definition='invalid-json'"; command.ExecuteNonQuery();
        }
        var vm = new WorkspaceViewModel(new WorkspaceService(repo, new(new SqliteContainerRepository(db)), new Log()), new(new SqliteContainerRepository(db)), new WindowsContainerItemPlatformService());
        Assert.False(vm.Reload()); Assert.Contains("No se pudo", vm.Status);
        Assert.Throws<System.Text.Json.JsonException>(repo.Load);
        using (File.Open(db, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }
    [Fact]
    public void Migration_v1_preserves_container_library_and_adds_shared_workspace()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path, "test.db");
        var cs = new ContainerService(new SqliteContainerRepository(db)); var c = cs.Create("Existing").Value!;
        cs.AddReference(c.Id, ContainerItemKind.SystemResource, "shell:AppsFolder", "Apps");
        using (var connection = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "DROP TABLE workspace_state; PRAGMA user_version=1"; command.ExecuteNonQuery();
        }
        var workspace = new SqliteWorkspaceRepository(db); Assert.Empty(workspace.Load().Categories);
        Assert.Equal("Existing", cs.Get(c.Id)!.Name); Assert.Single(cs.Get(c.Id)!.Items);
        workspace.Save(NexWorkspace.Empty); Assert.Single(cs.GetAll());
    }
    [Fact]
    public void Session_and_missing_resources_survive_restart_without_opening_shell()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path, "test.db");
        var cs = new ContainerService(new SqliteContainerRepository(db)); var c = cs.Create("Session").Value!;
        var ws = new WorkspaceService(new SqliteWorkspaceRepository(db), cs, new Log());
        ws.AddCategory("Missing"); var category = ws.Load().Categories[0];
        Assert.True(ws.AddReference(category.Id, ContainerItemKind.File, Path.Combine(scope.Path, "missing.txt"), "Missing").Succeeded);
        ws.Change(w => w with { Session = w.Session with { OpenContainers = [c.Id], RestoreContainers = true } });
        var vm = new WorkspaceViewModel(ws, cs, new WindowsContainerItemPlatformService()); Assert.True(vm.Reload());
        Assert.Equal(c.Id, vm.Snapshot.Session.OpenContainers.Single()); Assert.False(vm.Items(category = ws.Load().Categories[0]).Single().Status.Exists);
        vm.Open(category.Items[0]); Assert.Contains("recurso", vm.Status, StringComparison.OrdinalIgnoreCase);
    }
}
