using Microsoft.Data.Sqlite;
using NexSpace.Application;
using NexSpace.Infrastructure;
using Xunit;
namespace NexSpace.IntegrationTests;

public sealed class Phase4Tests
{
    [Fact]
    public void Failed_profile_write_preserves_active_data_and_releases_database()
    {
        using var scope = new IsolatedDirectory();
        var db = Path.Combine(scope.Path,"fault.db");
        var containers = new ContainerService(new SqliteContainerRepository(db));
        var original = containers.Create("Preserved").Value!;
        var profiles = new SqliteProfileRepository(db);
        var active = profiles.Load().ActiveId;
        using (var connection = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER reject_profile BEFORE INSERT ON profiles BEGIN SELECT RAISE(FAIL,'isolated failure'); END";
            command.ExecuteNonQuery();
        }
        Assert.ThrowsAny<Exception>(() => profiles.Create("Rejected",true));
        Assert.Equal(active,profiles.Load().ActiveId);
        Assert.Equal(original.Id,containers.GetAll().Single().Id);
        using (File.Open(db,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) { }
    }

    [Fact]
    public void Backup_restores_repeated_sessions_in_another_isolated_database()
    {
        using var scope = new IsolatedDirectory();
        var db = Path.Combine(scope.Path,"source.db");
        var containers = new ContainerService(new SqliteContainerRepository(db));
        for (var i=0;i<20;i++) containers.Create("Container "+i);
        var profiles = new SqliteProfileRepository(db);
        var active = profiles.Load().ActiveId; var alternate = profiles.Create("Alternate",true);
        for (var i=0;i<50;i++) { profiles.Switch(alternate.Id); profiles.Switch(active); }
        var backup = profiles.Backup(); var restored = Path.Combine(scope.Path,"restored.db");
        File.Copy(backup,restored);
        Assert.Equal(active,new SqliteProfileRepository(restored).Load().ActiveId);
        Assert.Equal(20,new SqliteContainerRepository(restored).GetAll().Count);
    }
}
