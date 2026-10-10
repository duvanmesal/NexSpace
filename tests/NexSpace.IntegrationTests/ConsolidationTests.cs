using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Infrastructure;
using NexSpace.Windows;
using NexSpace.Presentation;

namespace NexSpace.IntegrationTests;

public sealed class ConsolidationTests
{
    [Fact]
    public void View_model_commands_persist_references_and_ignore_diagnostic_failures()
    {
        using var scope = new IsolatedDirectory();
        var file = Path.Combine(scope.Path, "first.txt");
        var other = Path.Combine(scope.Path, "second.txt");
        File.WriteAllText(file, "first original"); File.WriteAllText(other, "second original");
        var service = new ContainerService(new SqliteContainerRepository(Path.Combine(scope.Path, "vm.db")));
        var container = service.Create("View model").Value!;
        var vm = new ContainerViewModel(service, new WindowsContainerItemPlatformService(), container.Id, new FailingLog());
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        Assert.Equal(2, vm.AddReferences([new(file, "First", false), new(other, "Second", false)]).Added);
        Assert.Contains(nameof(vm.Name), notifications); Assert.Contains(nameof(vm.Status), notifications);
        vm.MoveDownCommand.Execute(vm.Items[0]);
        Assert.Equal("Second", vm.Items[0].DisplayName);
        vm.MoveUpCommand.Execute(vm.Items[1]);
        Assert.Equal("First", vm.Items[0].DisplayName);
        vm.MoveUpCommand.Execute(vm.Items[0]); Assert.Contains("no se pudo mover", vm.Status);
        vm.RemoveCommand.Execute(vm.Items[0]); Assert.Single(vm.Items);
        var duplicate = vm.AddReferences([new(other, "Duplicate", false)]);
        Assert.Equal(0, duplicate.Added); Assert.Contains("duplicate-reference", duplicate.Errors);
        Assert.Equal("first original", File.ReadAllText(file)); Assert.Equal("second original", File.ReadAllText(other));
        Assert.Single(service.Get(container.Id)!.Items);
    }

    private sealed class FailingLog : IEventLog
    { public void Write(string eventName, string detail, string? error = null) => throw new IOException("Test log unavailable"); }

    [Fact]
    public async Task Windows_storage_payload_requested_as_move_is_processed_only_as_references()
    {
        using var scope = new IsolatedDirectory();
        var source = Path.Combine(scope.Path, "payload.txt");
        File.WriteAllText(source, "unchanged");
        var folderPath = Directory.CreateDirectory(Path.Combine(scope.Path, "payload-folder")).FullName;
        var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(source);
        var folder = await global::Windows.Storage.StorageFolder.GetFolderFromPathAsync(folderPath);
        var package = new global::Windows.ApplicationModel.DataTransfer.DataPackage
        { RequestedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move };
        package.SetStorageItems(new global::Windows.Storage.IStorageItem[] { file, folder });
        var view = package.GetView();
        Assert.True(ReferenceDropProcessor.CanAccept(view.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)));
        var service = new ContainerService(new SqliteContainerRepository(Path.Combine(scope.Path, "payload.db")));
        var container = service.Create("Payload").Value!;
        var entries = (await view.GetStorageItemsAsync()).Select(item => new ReferenceDropEntry(item.Path, item.Name, item is global::Windows.Storage.IStorageFolder));
        Assert.Equal(2, new ReferenceDropProcessor(service).Process(container.Id, entries).Added);
        Assert.Equal("unchanged", File.ReadAllText(source));
        Assert.True(Directory.Exists(folderPath));
    }

    [Fact]
    public void Drop_processes_mixed_storage_metadata_and_never_changes_sources()
    {
        using var scope = new IsolatedDirectory();
        var file = Path.Combine(scope.Path, "nota.txt");
        File.WriteAllText(file, "immutable source");
        var folder = Directory.CreateDirectory(Path.Combine(scope.Path, "folder")).FullName;
        var link = Path.Combine(scope.Path, "item.LNK");
        File.WriteAllBytes(link, [1, 2, 3]); // Parser metadata test; not a Shell-launch fixture.
        var exe = Path.Combine(scope.Path, "app.EXE");
        File.WriteAllBytes(exe, [4, 5, 6]);
        var before = Directory.GetFiles(scope.Path).ToDictionary(path => path, File.ReadAllBytes);
        var repository = new SqliteContainerRepository(Path.Combine(scope.Path, "state.db"));
        var service = new ContainerService(repository);
        var container = service.Create("Drop").Value!;
        var result = new ReferenceDropProcessor(service).Process(container.Id,
        [new(file, "Nota", false), new(folder, "Carpeta", true), new(link, "Enlace", false), new(exe, "App", false),
         new(file, "Duplicado", false), new("relative.txt", "Inválida", false), new("https://example.invalid", "URL", false), new(file, "", false)]);
        Assert.Equal(4, result.Added);
        Assert.Equal(4, result.Errors.Count);
        Assert.Contains("duplicate-reference", result.Errors);
        Assert.Equal([ContainerItemKind.File, ContainerItemKind.Folder, ContainerItemKind.Shortcut, ContainerItemKind.Application], repository.Get(container.Id)!.Items.Select(item => item.Kind));
        Assert.True(service.Delete(container.Id).Succeeded);
        foreach (var original in before) Assert.Equal(original.Value, File.ReadAllBytes(original.Key));
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void Repeated_reopen_and_basic_load_preserve_many_containers_and_release_database()
    {
        using var scope = new IsolatedDirectory();
        var path = Path.Combine(scope.Path, "load.db");
        var repository = new SqliteContainerRepository(path);
        var watch = Stopwatch.StartNew();
        for (var c = 0; c < 12; c++)
        {
            var container = repository.Create(NexContainer.Create($"Container {c}", c) with { IsCollapsed = c % 2 == 0, AccentKey = "copper", Placement = new("test", c * 10, c * 20, 420, 340) });
            for (var i = 0; i < 20; i++) repository.AddReference(container.Id, ContainerItemKind.File, Path.Combine(scope.Path, $"source-{i}.txt"), $"Reference {i}");
        }
        for (var cycle = 0; cycle < 20; cycle++)
        {
            var records = new SqliteContainerRepository(path).GetAll();
            Assert.Equal(12, records.Count);
            Assert.All(records, record =>
            {
                Assert.Equal(20, record.Items.Count);
                Assert.Equal(Enumerable.Range(0, 20), record.Items.Select(item => item.SortOrder));
                Assert.Equal("copper", record.AccentKey);
                Assert.Equal(340, record.Placement.Height);
            });
            Assert.Equal(6, records.Count(record => record.IsCollapsed));
        }
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        Assert.True(watch.Elapsed < TimeSpan.FromMinutes(2)); // Generous regression bound, not a performance guarantee.
    }

    [Fact]
    public void Schema_failure_releases_connection_and_service_can_recover()
    {
        using var scope = new IsolatedDirectory();
        var path = Path.Combine(scope.Path, "future.db");
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version=99"; command.ExecuteNonQuery();
        }
        var repository = new SqliteContainerRepository(path);
        var service = new ContainerService(repository);
        for (var attempt = 0; attempt < 10; attempt++) Assert.False(service.Create("Invalid schema").Succeeded);
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version=0"; command.ExecuteNonQuery();
        }
        Assert.True(service.Create("Recovered").Succeeded);
    }

    [Fact]
    public void Denied_attributes_on_an_owned_test_file_are_reported_without_opening_shell()
    {
        using var scope = new IsolatedDirectory();
        var path = Path.Combine(scope.Path, "denied.txt");
        File.WriteAllText(path, "owned isolated fixture");
        var info = new FileInfo(path);
        var originalAcl = info.GetAccessControl();
        var deniedAcl = info.GetAccessControl();
        var sid = WindowsIdentity.GetCurrent().User!;
        deniedAcl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.ReadAttributes | FileSystemRights.ReadData, AccessControlType.Deny));
        try
        {
            info.SetAccessControl(deniedAcl);
            var item = new ContainerItemReference(Guid.NewGuid(), ContainerItemKind.File, path, path, "Denied", 0, DateTimeOffset.UtcNow);
            var platform = new WindowsContainerItemPlatformService();
            Assert.Equal("access-denied", platform.Inspect(item).Code);
            Assert.Equal("access-denied", platform.Open(item).Code);
        }
        finally
        {
            var restored = new FileSecurity();
            restored.SetSecurityDescriptorSddlForm(originalAcl.GetSecurityDescriptorSddlForm(AccessControlSections.Access), AccessControlSections.Access);
            info.SetAccessControl(restored);
        }
        Assert.Equal("owned isolated fixture", File.ReadAllText(path));
    }
}

internal sealed class IsolatedDirectory : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("NexSpace-Consolidation-");
    public string Path => _directory.FullName;
    public void Dispose() => _directory.Delete(recursive: true);
}
