using Microsoft.Data.Sqlite;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Infrastructure;
using Xunit;
namespace NexSpace.IntegrationTests;

public sealed class ProfilePersistenceTests
{
    [Fact] public void Switch_duplicate_rename_delete_preserve_original_and_geometry()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path, "profiles.db"); var original = Path.Combine(scope.Path,"original.txt"); File.WriteAllText(original,"Immutable");
        var cs = new ContainerService(new SqliteContainerRepository(db)); var c = cs.Create("Work").Value!;
        cs.AddReference(c.Id, ContainerItemKind.File,original,"Original"); cs.UpdatePresentation(c.Id,collapsed:true,positionLocked:true);
        var wr = new SqliteWorkspaceRepository(db); wr.Save(NexWorkspace.Empty with { Session = NexWorkspace.Empty.Session with { OpenContainers = [c.Id] }, Theme = ThemeSettings.Default with { Preset = ThemePreset.Light } });
        var profiles = new SqliteProfileRepository(db); var initial = profiles.Load().ActiveId; var copy = profiles.Create("Copy",true); var empty = profiles.Create("Empty",false);
        profiles.Switch(empty.Id); Assert.Empty(cs.GetAll()); Assert.Empty(wr.Load().Session.OpenContainers);
        cs.Create("Different"); profiles.Switch(copy.Id); var restored = cs.Get(c.Id)!; Assert.True(restored.IsCollapsed); Assert.True(restored.IsPositionLocked); Assert.Single(restored.Items);
        Assert.Equal(c.Id,wr.Load().Session.OpenContainers.Single()); Assert.Equal(ThemePreset.Light,wr.Load().Theme.Preset);
        cs.Rename(c.Id,"Independent"); profiles.Switch(initial); Assert.Equal("Work",cs.Get(c.Id)!.Name);
        profiles.Rename(copy.Id,"Renamed"); profiles.Delete(copy.Id); Assert.DoesNotContain(profiles.Load().Profiles,p=>p.Id==copy.Id); Assert.Equal("Immutable",File.ReadAllText(original));
        using (File.Open(db, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }
    [Fact] public void Repeated_profile_switches_restore_linked_categories_and_order()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path,"profiles.db"); var cs = new ContainerService(new SqliteContainerRepository(db)); var c = cs.Create("Work").Value!;
        cs.AddReference(c.Id,ContainerItemKind.SystemResource,"shell:AppsFolder","Apps"); cs.AddReference(c.Id,ContainerItemKind.SystemResource,"shell:Downloads","Downloads");
        var wr = new SqliteWorkspaceRepository(db); wr.Save(NexWorkspace.Empty with { Categories = [new(Guid.NewGuid(),"Linked","\uE8B7",c.Id,[])] });
        var p = new SqliteProfileRepository(db); var first = p.Load().ActiveId; var next = p.Create("Next",true);
        for (var i=0;i<30;i++) { p.Switch(next.Id); Assert.Equal(c.Id,wr.Load().Categories.Single().ContainerId); p.Switch(first); Assert.Equal(new[]{"Apps","Downloads"},cs.Get(c.Id)!.Items.Select(x=>x.DisplayName)); }
        Assert.Equal(2,new SqliteProfileRepository(db).Load().Profiles.Count);
    }
    [Fact] public void Corrupt_target_rolls_back_without_losing_active_data()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path,"profiles.db"); var cs = new ContainerService(new SqliteContainerRepository(db)); var c = cs.Create("Safe").Value!;
        var p = new SqliteProfileRepository(db); var active = p.Load().ActiveId; var broken = p.Create("Broken",false);
        using (var connection = new SqliteConnection($"Data Source={db};Pooling=False")) { connection.Open(); using var q = connection.CreateCommand(); q.CommandText="UPDATE profiles SET snapshot='invalid-json' WHERE id=$id"; q.Parameters.AddWithValue("$id",broken.Id.ToString("D")); q.ExecuteNonQuery(); }
        Assert.ThrowsAny<Exception>(()=>p.Switch(broken.Id)); Assert.Equal(active,p.Load().ActiveId); Assert.Equal("Safe",cs.Get(c.Id)!.Name);
        Assert.ThrowsAny<Exception>(()=>p.Switch(Guid.NewGuid())); Assert.Single(cs.GetAll());
    }
    [Fact] public void Active_default_deletion_and_duplicate_names_rejected()
    {
        using var scope = new IsolatedDirectory(); var p = new SqliteProfileRepository(Path.Combine(scope.Path,"profiles.db")); var active=p.Load().ActiveId;
        Assert.Throws<InvalidOperationException>(()=>p.Delete(active)); p.Create("Other",false); Assert.ThrowsAny<Exception>(()=>p.Create("other",true));
        Assert.Equal(2,p.Load().Profiles.Count); Assert.ThrowsAny<Exception>(()=>p.SetStartup(Guid.NewGuid(),false)); Assert.True(p.Load().RestoreLast);
    }
    [Fact] public void V2_migration_preserves_rows_and_creates_readable_pre_migration_backup()
    {
        using var scope = new IsolatedDirectory(); var db=Path.Combine(scope.Path,"v2.db"); var cs = new ContainerService(new SqliteContainerRepository(db)); var c=cs.Create("Existing").Value!; cs.AddReference(c.Id,ContainerItemKind.SystemResource,"shell:AppsFolder","Apps");
        using (var connection=new SqliteConnection($"Data Source={db};Pooling=False")) { connection.Open(); using var q=connection.CreateCommand(); q.CommandText="DROP TABLE profile_state; DROP TABLE profiles; PRAGMA user_version=2";q.ExecuteNonQuery(); }
        var p=new SqliteProfileRepository(db); Assert.Single(p.Load().Profiles); Assert.Equal(c.Id,cs.GetAll().Single().Id); Assert.Single(cs.Get(c.Id)!.Items);
        var backup=Directory.GetFiles(scope.Path,"*.pre-v3-*.bak").Single();
        using var source=new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False");source.Open();using var cmd=source.CreateCommand();cmd.CommandText="PRAGMA user_version";Assert.Equal(2L,cmd.ExecuteScalar());cmd.CommandText="SELECT COUNT(*) FROM container_items";Assert.Equal(1L,cmd.ExecuteScalar());
    }
    [Fact] public void Backups_are_unique_consistent_and_do_not_overwrite_active()
    {
        using var scope=new IsolatedDirectory();var db=Path.Combine(scope.Path,"profiles.db");var p=new SqliteProfileRepository(db);p.Create("Backup",false);var a=p.Backup();var b=p.Backup();Assert.NotEqual(a,b);
        Assert.Equal(2,new SqliteProfileRepository(a).Load().Profiles.Count);Assert.Equal(2,p.Load().Profiles.Count);
        using (File.Open(a,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) { }
    }
    [Fact] public void Startup_global_preferences_persist_without_registering_windows_startup()
    {
        using var scope=new IsolatedDirectory();var db=Path.Combine(scope.Path,"profiles.db");var p=new SqliteProfileRepository(db);var second=p.Create("Default",false);p.SetStartup(second.Id,false);
        var read=new SqliteProfileRepository(db).Load();Assert.Equal(second.Id,read.DefaultId);Assert.False(read.RestoreLast);Assert.NotEqual(second.Id,read.ActiveId);
    }
    [Fact] public void Conflicting_shared_reference_identity_rolls_back()
    {
        using var scope = new IsolatedDirectory(); var db = Path.Combine(scope.Path,"profiles.db");
        var cs = new ContainerService(new SqliteContainerRepository(db)); var original = cs.Create("Safe").Value!;
        var p = new SqliteProfileRepository(db); var active = p.Load().ActiveId; var broken = p.Create("Conflict",false);
        var id = Guid.NewGuid(); var item = new ContainerItemReference(id,ContainerItemKind.SystemResource,"shell:AppsFolder",ContainerRules.NormalizeTargetKey("shell:AppsFolder"),"Apps",0,DateTimeOffset.UtcNow);
        var first = NexContainer.Create("First") with { Items = [item] };
        var second = NexContainer.Create("Second") with { Items = [item with { Target="shell:Downloads", TargetKey=ContainerRules.NormalizeTargetKey("shell:Downloads") }] };
        var snapshot = new ProfileSnapshot([first,second],NexWorkspace.Empty);
        using (var c = new SqliteConnection($"Data Source={db};Pooling=False")) { c.Open(); using var q=c.CreateCommand();q.CommandText="UPDATE profiles SET snapshot=$json WHERE id=$id";q.Parameters.AddWithValue("$json",System.Text.Json.JsonSerializer.Serialize(snapshot));q.Parameters.AddWithValue("$id",broken.Id.ToString("D"));q.ExecuteNonQuery(); }
        Assert.Throws<InvalidDataException>(()=>p.Switch(broken.Id)); Assert.Equal(active,p.Load().ActiveId); Assert.Equal(original.Id,cs.GetAll().Single().Id);
    }
}
