using Microsoft.Data.Sqlite;
using System.Text.Json;
using NexSpace.Application;
using NexSpace.Domain;
namespace NexSpace.Infrastructure;

/// <summary>The live v2 tables are the active profile; archived profiles are typed snapshots. Switching is one transaction.</summary>
public sealed class SqliteProfileRepository(string databasePath) : IProfileRepository
{
    private readonly SqliteContainerRepository _database = new(databasePath);
    public ProfileState Load()
    {
        using var c = _database.Open(); return ReadState(c);
    }
    private static ProfileState ReadState(SqliteConnection c)
    {
        using var q = c.CreateCommand(); q.CommandText = "SELECT active_id,default_id,restore_last FROM profile_state WHERE id=1";
        using var r = q.ExecuteReader(); if (!r.Read()) throw new InvalidDataException("Missing profile state.");
        var active = Guid.Parse(r.GetString(0)); var fallback = Guid.Parse(r.GetString(1)); var restore = r.GetInt32(2) != 0; r.Close();
        q.CommandText = "SELECT id,name FROM profiles ORDER BY name COLLATE NOCASE";
        using var names = q.ExecuteReader(); var profiles = new List<NexProfile>();
        while (names.Read()) profiles.Add(new(Guid.Parse(names.GetString(0)), names.GetString(1)));
        return new(active, fallback, restore, profiles);
    }
    private static ProfileSnapshot Capture(SqliteConnection c)
    {
        using var q = c.CreateCommand(); q.CommandText = "SELECT id FROM containers ORDER BY sort_order,name COLLATE NOCASE";
        var ids = new List<Guid>(); using (var r = q.ExecuteReader()) while (r.Read()) ids.Add(Guid.Parse(r.GetString(0)));
        var containers = ids.Select(id => SqliteContainerRepository.Load(c, id)!).ToArray();
        q.CommandText = "SELECT definition FROM workspace_state WHERE id=1";
        var json = q.ExecuteScalar() as string;
        var workspace = json is null ? NexWorkspace.Empty : JsonSerializer.Deserialize<NexWorkspace>(json) ?? throw new InvalidDataException("Invalid workspace.");
        return new(containers, WorkspaceRules.Validate(workspace));
    }
    public NexProfile Create(string name, bool duplicateActive)
    {
        name = ContainerRules.NormalizeName(name);
        using var c = _database.Open(); using var tx = c.BeginTransaction();
        if (ReadState(c).Profiles.Count >= 100) throw new InvalidOperationException("Máximo 100 perfiles.");
        var snapshot = duplicateActive ? Capture(c) : new ProfileSnapshot([], NexWorkspace.Empty);
        var profile = new NexProfile(Guid.NewGuid(), name);
        Execute(c, tx, "INSERT INTO profiles(id,name,snapshot) VALUES($id,$name,$json)", ("$id", profile.Id.ToString("D")), ("$name", name), ("$json", JsonSerializer.Serialize(snapshot)));
        tx.Commit(); return profile;
    }
    public void Rename(Guid id, string name)
    {
        using var c = _database.Open(); using var tx = c.BeginTransaction();
        if (Execute(c, tx, "UPDATE profiles SET name=$name WHERE id=$id", ("$id", id.ToString("D")), ("$name", ContainerRules.NormalizeName(name))) != 1) throw new KeyNotFoundException();
        tx.Commit();
    }
    public void Delete(Guid id)
    {
        using var c = _database.Open(); using var tx = c.BeginTransaction(); var state = ReadState(c);
        if (id == state.ActiveId || id == state.DefaultId) throw new InvalidOperationException("No se elimina el perfil activo ni el predeterminado. Cambia primero ambos.");
        if (Execute(c, tx, "DELETE FROM profiles WHERE id=$id", ("$id", id.ToString("D"))) != 1) throw new KeyNotFoundException();
        tx.Commit();
    }
    public void SetStartup(Guid defaultId, bool restoreLast)
    {
        using var c = _database.Open(); using var tx = c.BeginTransaction();
        Execute(c, tx, "UPDATE profile_state SET default_id=$id,restore_last=$restore WHERE id=1", ("$id", defaultId.ToString("D")), ("$restore", restoreLast ? 1 : 0)); tx.Commit();
    }
    public void Switch(Guid id)
    {
        using var c = _database.Open(); using var tx = c.BeginTransaction(); var state = ReadState(c);
        if (id == state.ActiveId) return;
        using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = "SELECT snapshot FROM profiles WHERE id=$id"; q.Parameters.AddWithValue("$id", id.ToString("D"));
        var json = q.ExecuteScalar() as string ?? throw new InvalidDataException("Perfil ausente o corrupto; no se alteraron los datos.");
        var target = JsonSerializer.Deserialize<ProfileSnapshot>(json) ?? throw new InvalidDataException("Perfil corrupto.");
        target = target with { Workspace = WorkspaceRules.Validate(target.Workspace) };
        if (target.Containers is null || target.Containers.Count > 1000) throw new InvalidDataException("Contenedores no válidos.");
        var identities = new Dictionary<Guid, (ContainerItemKind Kind, string Target, string Key)>();
        var containerIds = new HashSet<Guid>();
        foreach (var container in target.Containers)
        {
            if (container.Id == Guid.Empty || !containerIds.Add(container.Id) || container.Items is null || container.Items.Count > 1000 ||
                container.Placement is null || !Enum.IsDefined(container.ViewMode) || !ContainerRules.IsAllowedAccent(container.AccentKey))
                throw new InvalidDataException("Identidad o presentación de contenedor no válida.");
            ContainerRules.NormalizeName(container.Name);
            var references = new HashSet<Guid>();
            foreach (var item in container.Items)
            {
                WorkspaceRules.ValidateReference(item.Kind, item.Target, item.DisplayName);
                if (item.Id == Guid.Empty || !references.Add(item.Id) || item.TargetKey != ContainerRules.NormalizeTargetKey(item.Target))
                    throw new InvalidDataException("Identidad de referencia no válida.");
                var identity = (item.Kind, item.Target, item.TargetKey);
                if (identities.TryGetValue(item.Id, out var previous) && previous != identity)
                    throw new InvalidDataException("Una identidad compartida no puede apuntar a destinos diferentes.");
                identities[item.Id] = identity;
            }
        }
        Execute(c, tx, "UPDATE profiles SET snapshot=$json WHERE id=$id", ("$json", JsonSerializer.Serialize(Capture(c))), ("$id", state.ActiveId.ToString("D")));
        Execute(c, tx, "DELETE FROM container_items; DELETE FROM containers; DELETE FROM items; DELETE FROM workspace_state");
        foreach (var container in target.Containers)
        {
            using var insert = c.CreateCommand(); insert.Transaction = tx;
            insert.CommandText = "INSERT INTO containers VALUES($id,$name,$sort,$collapsed,$locked,$view,$accent,$monitor,$x,$y,$width,$height,$created,$updated)";
            SqliteContainerRepository.BindContainer(insert, container); insert.ExecuteNonQuery();
            foreach (var item in container.Items)
            {
                Execute(c, tx, "INSERT OR IGNORE INTO items VALUES($id,$kind,$target,$key,$added)", ("$id", item.Id.ToString("D")), ("$kind", (int)item.Kind), ("$target", item.Target), ("$key", item.TargetKey), ("$added", item.AddedUtc.ToString("O")));
                Execute(c, tx, "INSERT INTO container_items VALUES($container,$id,$sort,$name)", ("$container", container.Id.ToString("D")), ("$id", item.Id.ToString("D")), ("$sort", item.SortOrder), ("$name", item.DisplayName));
            }
        }
        Execute(c, tx, "INSERT INTO workspace_state VALUES(1,$revision,$json)", ("$revision", target.Workspace.Revision), ("$json", JsonSerializer.Serialize(target.Workspace)));
        Execute(c, tx, "UPDATE profile_state SET active_id=$id WHERE id=1", ("$id", id.ToString("D"))); tx.Commit();
    }
    public string Backup()
    {
        using var source = _database.Open(); var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, "backups"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "nexspace-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".db");
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        target.Open(); source.BackupDatabase(target); return path;
    }
    private static int Execute(SqliteConnection c, SqliteTransaction tx, string sql, params (string Key, object Value)[] values)
    {
        using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = sql;
        foreach (var value in values) q.Parameters.AddWithValue(value.Key, value.Value); return q.ExecuteNonQuery();
    }
}
