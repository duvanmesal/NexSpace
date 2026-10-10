using Microsoft.Data.Sqlite;
using NexSpace.Application;
using NexSpace.Domain;

namespace NexSpace.Infrastructure;

/// <summary>Persists container metadata and resource references; it never operates on referenced files.</summary>
public sealed class SqliteContainerRepository(string databasePath) : IContainerRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = Path.GetFullPath(databasePath),
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        Pooling = false
    }.ToString();

    public IReadOnlyList<NexContainer> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM containers ORDER BY sort_order, name COLLATE NOCASE";
        var ids = new List<Guid>();
        using (var reader = command.ExecuteReader()) while (reader.Read()) ids.Add(Guid.Parse(reader.GetString(0)));
        return ids.Select(id => Load(connection, id)!).ToArray();
    }

    public NexContainer? Get(Guid id)
    {
        using var connection = Open();
        return Load(connection, id);
    }

    public NexContainer Create(NexContainer container)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO containers(id,name,sort_order,is_collapsed,is_position_locked,view_mode,accent_key,monitor_key,x,y,width,height,created_utc,updated_utc) VALUES($id,$name,$sort,$collapsed,$locked,$view,$accent,$monitor,$x,$y,$width,$height,$created,$updated)";
        BindContainer(command, container);
        command.ExecuteNonQuery();
        transaction.Commit();
        return container;
    }

    public NexContainer Update(NexContainer container)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE containers SET name=$name,sort_order=$sort,is_collapsed=$collapsed,is_position_locked=$locked,view_mode=$view,accent_key=$accent,monitor_key=$monitor,x=$x,y=$y,width=$width,height=$height,updated_utc=$updated WHERE id=$id";
        BindContainer(command, container);
        if (command.ExecuteNonQuery() == 0) throw new KeyNotFoundException("Container not found.");
        return Load(connection, container.Id)!;
    }

    public bool Delete(Guid id)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM containers WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        var deleted = command.ExecuteNonQuery() > 0;
        using var cleanup = connection.CreateCommand();
        cleanup.Transaction = transaction;
        cleanup.CommandText = "DELETE FROM items WHERE NOT EXISTS (SELECT 1 FROM container_items WHERE item_id=items.id)";
        cleanup.ExecuteNonQuery();
        transaction.Commit();
        return deleted;
    }

    public ContainerItemReference AddReference(Guid containerId, ContainerItemKind kind, string target, string displayName)
    {
        var key = ContainerRules.NormalizeTargetKey(target);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var find = connection.CreateCommand();
        find.Transaction = transaction;
        find.CommandText = "SELECT id FROM items WHERE target_key=$key";
        find.Parameters.AddWithValue("$key", key);
        var existing = find.ExecuteScalar() as string;
        var itemId = existing is null ? Guid.NewGuid() : Guid.Parse(existing);
        if (existing is null)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO items(id,kind,target,target_key,added_utc) VALUES($id,$kind,$target,$key,$added)";
            insert.Parameters.AddWithValue("$id", itemId.ToString("D"));
            insert.Parameters.AddWithValue("$kind", (int)kind);
            insert.Parameters.AddWithValue("$target", target);
            insert.Parameters.AddWithValue("$key", key);
            insert.Parameters.AddWithValue("$added", DateTimeOffset.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
        }
        using var link = connection.CreateCommand();
        link.Transaction = transaction;
        link.CommandText = "INSERT INTO container_items(container_id,item_id,sort_order,display_name) SELECT $container,$item,COALESCE(MAX(sort_order)+1,0),$name FROM container_items WHERE container_id=$container";
        link.Parameters.AddWithValue("$container", containerId.ToString("D"));
        link.Parameters.AddWithValue("$item", itemId.ToString("D"));
        link.Parameters.AddWithValue("$name", displayName);
        link.ExecuteNonQuery();
        transaction.Commit();
        using var read = connection.CreateCommand();
        read.CommandText = "SELECT i.kind,i.target,i.target_key,ci.display_name,ci.sort_order,i.added_utc FROM items i JOIN container_items ci ON ci.item_id=i.id WHERE i.id=$id AND ci.container_id=$container";
        read.Parameters.AddWithValue("$id", itemId.ToString("D"));
        read.Parameters.AddWithValue("$container", containerId.ToString("D"));
        using var reader = read.ExecuteReader();
        reader.Read();
        return new(itemId, (ContainerItemKind)reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), DateTimeOffset.Parse(reader.GetString(5)));
    }

    public bool RemoveReference(Guid containerId, Guid itemId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var remove = connection.CreateCommand();
        remove.Transaction = transaction;
        remove.CommandText = "DELETE FROM container_items WHERE container_id=$container AND item_id=$item";
        remove.Parameters.AddWithValue("$container", containerId.ToString("D"));
        remove.Parameters.AddWithValue("$item", itemId.ToString("D"));
        var removed = remove.ExecuteNonQuery() > 0;
        using var cleanup = connection.CreateCommand();
        cleanup.Transaction = transaction;
        cleanup.CommandText = "DELETE FROM items WHERE id=$item AND NOT EXISTS (SELECT 1 FROM container_items WHERE item_id=$item)";
        cleanup.Parameters.AddWithValue("$item", itemId.ToString("D"));
        cleanup.ExecuteNonQuery();
        transaction.Commit();
        return removed;
    }

    public bool MoveReference(Guid containerId, Guid itemId, int offset)
    {
        if (offset == 0) return false;
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT item_id,sort_order FROM container_items WHERE container_id=$container ORDER BY sort_order,item_id";
        command.Parameters.AddWithValue("$container", containerId.ToString("D"));
        var rows = new List<(string Id, int Order)>();
        using (var reader = command.ExecuteReader()) while (reader.Read()) rows.Add((reader.GetString(0), reader.GetInt32(1)));
        var index = rows.FindIndex(row => row.Id == itemId.ToString("D"));
        var next = index + Math.Sign(offset);
        if (index < 0 || next < 0 || next >= rows.Count) return false;
        (rows[index], rows[next]) = (rows[next], rows[index]);
        for (var i = 0; i < rows.Count; i++)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE container_items SET sort_order=$order WHERE container_id=$container AND item_id=$item";
            update.Parameters.AddWithValue("$order", i);
            update.Parameters.AddWithValue("$container", containerId.ToString("D"));
            update.Parameters.AddWithValue("$item", rows[i].Id);
            update.ExecuteNonQuery();
        }
        transaction.Commit();
        return true;
    }

    public ContainerItemReference UpdateReference(Guid containerId, ContainerItemReference item)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE container_items SET display_name=$name WHERE item_id=$item AND container_id=$container";
        command.Parameters.AddWithValue("$name", item.DisplayName);
        command.Parameters.AddWithValue("$item", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$container", containerId.ToString("D"));
        if (command.ExecuteNonQuery() == 0) throw new KeyNotFoundException("Reference not found.");
        return item;
    }

    private SqliteConnection Open()
    {
        var path = new SqliteConnectionStringBuilder(_connectionString).DataSource;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            using (var pragma = connection.CreateCommand()) { pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000"; pragma.ExecuteNonQuery(); }
            EnsureSchema(connection);
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        var current = Convert.ToInt32(version.ExecuteScalar());
        if (current > 1) throw new InvalidOperationException("Database schema is newer than this application.");
        if (current == 1) return;
        using var transaction = connection.BeginTransaction();
        using var schema = connection.CreateCommand();
        schema.Transaction = transaction;
        schema.CommandText = """
            CREATE TABLE containers(id TEXT PRIMARY KEY,name TEXT NOT NULL COLLATE NOCASE UNIQUE,sort_order INTEGER NOT NULL,is_collapsed INTEGER NOT NULL,is_position_locked INTEGER NOT NULL,view_mode INTEGER NOT NULL,accent_key TEXT NOT NULL,monitor_key TEXT NOT NULL,x INTEGER NOT NULL,y INTEGER NOT NULL,width INTEGER NOT NULL,height INTEGER NOT NULL,created_utc TEXT NOT NULL,updated_utc TEXT NOT NULL);
            CREATE TABLE items(id TEXT PRIMARY KEY,kind INTEGER NOT NULL,target TEXT NOT NULL,target_key TEXT NOT NULL UNIQUE,added_utc TEXT NOT NULL);
            CREATE TABLE container_items(container_id TEXT NOT NULL REFERENCES containers(id) ON DELETE CASCADE,item_id TEXT NOT NULL REFERENCES items(id) ON DELETE CASCADE,sort_order INTEGER NOT NULL,display_name TEXT NOT NULL,PRIMARY KEY(container_id,item_id));
            CREATE INDEX ix_container_items_order ON container_items(container_id,sort_order);
            PRAGMA user_version=1;
            """;
        schema.ExecuteNonQuery();
        transaction.Commit();
    }

    private static NexContainer? Load(SqliteConnection connection, Guid id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name,sort_order,is_collapsed,is_position_locked,view_mode,accent_key,monitor_key,x,y,width,height,created_utc,updated_utc FROM containers WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        var container = new NexContainer(id, reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2) != 0, reader.GetInt32(3) != 0,
            (ContainerViewMode)reader.GetInt32(4), reader.GetString(5), new(reader.GetString(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10)),
            DateTimeOffset.Parse(reader.GetString(11)), DateTimeOffset.Parse(reader.GetString(12)), Array.Empty<ContainerItemReference>());
        reader.Close();
        using var items = connection.CreateCommand();
        items.CommandText = "SELECT i.id,i.kind,i.target,i.target_key,ci.display_name,ci.sort_order,i.added_utc FROM items i JOIN container_items ci ON ci.item_id=i.id WHERE ci.container_id=$id ORDER BY ci.sort_order,i.id";
        items.Parameters.AddWithValue("$id", id.ToString("D"));
        using var itemReader = items.ExecuteReader();
        var references = new List<ContainerItemReference>();
        while (itemReader.Read()) references.Add(new(Guid.Parse(itemReader.GetString(0)), (ContainerItemKind)itemReader.GetInt32(1), itemReader.GetString(2), itemReader.GetString(3), itemReader.GetString(4), itemReader.GetInt32(5), DateTimeOffset.Parse(itemReader.GetString(6))));
        return container with { Items = references };
    }

    private static void BindContainer(SqliteCommand command, NexContainer c)
    {
        command.Parameters.AddWithValue("$id", c.Id.ToString("D")); command.Parameters.AddWithValue("$name", c.Name); command.Parameters.AddWithValue("$sort", c.SortOrder);
        command.Parameters.AddWithValue("$collapsed", c.IsCollapsed ? 1 : 0); command.Parameters.AddWithValue("$locked", c.IsPositionLocked ? 1 : 0);
        command.Parameters.AddWithValue("$view", (int)c.ViewMode); command.Parameters.AddWithValue("$accent", c.AccentKey); command.Parameters.AddWithValue("$monitor", c.Placement.MonitorKey);
        command.Parameters.AddWithValue("$x", c.Placement.X); command.Parameters.AddWithValue("$y", c.Placement.Y); command.Parameters.AddWithValue("$width", c.Placement.Width); command.Parameters.AddWithValue("$height", c.Placement.Height);
        command.Parameters.AddWithValue("$created", c.CreatedUtc.ToString("O")); command.Parameters.AddWithValue("$updated", c.UpdatedUtc.ToString("O"));
    }
}
