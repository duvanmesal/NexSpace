using NexSpace.Domain;
using NexSpace.Infrastructure;
using NexSpace.Application;

namespace NexSpace.IntegrationTests;

public sealed class ContainerPersistenceTests
{
    [Fact]
    public void Empty_repository_initializes_database_and_returns_empty_collection()
    {
        var root = Path.Combine(Path.GetTempPath(), "NexSpace-Integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var database = Path.Combine(root, "empty.db");
            Assert.Empty(new SqliteContainerRepository(database).GetAll());
            Assert.True(File.Exists(database));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void References_persist_share_reorder_and_remove_without_touching_original_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "NexSpace-Integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "reference-only.txt");
            var original = "the source must remain byte-for-byte unchanged";
            File.WriteAllText(source, original);
            var database = Path.Combine(root, "isolated.db");
            var repository = new SqliteContainerRepository(database);
            var first = repository.Create(NexContainer.Create("Trabajo"));
            var second = repository.Create(NexContainer.Create("Lectura"));
            var reference = repository.AddReference(first.Id, ContainerItemKind.File, source, "source");
            repository.AddReference(first.Id, ContainerItemKind.File, Path.Combine(root, "other.txt"), "other");
            repository.AddReference(second.Id, ContainerItemKind.File, source, "same source");

            Assert.True(repository.MoveReference(first.Id, reference.Id, 1));
            var reloaded = new SqliteContainerRepository(database);
            Assert.Equal("other", Assert.Single(reloaded.Get(first.Id)!.Items, item => item.SortOrder == 0).DisplayName);
            Assert.Equal(reference.Id, Assert.Single(reloaded.Get(second.Id)!.Items).Id);
            Assert.Equal("same source", Assert.Single(reloaded.Get(second.Id)!.Items).DisplayName);

            Assert.True(reloaded.RemoveReference(first.Id, reference.Id));
            Assert.True(File.Exists(source));
            Assert.Equal(original, File.ReadAllText(source));
            Assert.Equal(reference.Id, Assert.Single(reloaded.Get(second.Id)!.Items).Id);
            Assert.True(reloaded.Delete(first.Id));
            Assert.True(File.Exists(source));
            Assert.Equal(original, File.ReadAllText(source));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Container_names_are_unique_case_insensitively_and_invalid_names_are_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "NexSpace-Integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var repository = new SqliteContainerRepository(Path.Combine(root, "isolated.db"));
            repository.Create(NexContainer.Create("Trabajo"));
            Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => repository.Create(NexContainer.Create("trabajo")));
            Assert.Throws<ArgumentException>(() => NexContainer.Create("   "));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Position_lock_preserves_coordinates_but_still_persists_resize()
    {
        var root = Path.Combine(Path.GetTempPath(), "NexSpace-Integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = new ContainerService(new SqliteContainerRepository(Path.Combine(root, "locked.db")));
            var container = service.Create("Fijado").Value!;
            Assert.True(service.UpdatePresentation(container.Id, positionLocked: true).Succeeded);
            var changed = service.SavePlacement(container.Id, new("new-monitor", 900, 800, 620, 480));
            Assert.True(changed.Succeeded);
            Assert.Equal(container.Placement.X, changed.Value!.Placement.X);
            Assert.Equal(container.Placement.Y, changed.Value.Placement.Y);
            Assert.Equal(container.Placement.Width + 200, changed.Value.Placement.Width);
            Assert.Equal("", changed.Value.Placement.MonitorKey);
            var recovered = service.SavePlacement(container.Id, new("primary", 20, 30, 620, 480), recoverPosition: true);
            Assert.True(recovered.Succeeded);
            Assert.True(recovered.Value!.IsPositionLocked);
            Assert.Equal(20, recovered.Value.Placement.X);
            Assert.Equal(30, recovered.Value.Placement.Y);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
