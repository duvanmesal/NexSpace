using System.Text.Json;
using NexSpace.Application;
using NexSpace.Domain;

namespace NexSpace.Infrastructure;

/// <summary>Versioned workspace document, atomically saved with optimistic concurrency in the shared database.</summary>
public sealed class SqliteWorkspaceRepository(string databasePath) : IWorkspaceRepository
{
    private readonly SqliteContainerRepository _database = new(databasePath);
    public NexWorkspace Load()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT revision,definition FROM workspace_state WHERE id=1";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return NexWorkspace.Empty;
        var value = JsonSerializer.Deserialize<NexWorkspace>(reader.GetString(1)) ?? throw new InvalidDataException("Empty workspace definition.");
        return WorkspaceRules.Validate(value with { Revision = reader.GetInt64(0) });
    }
    public NexWorkspace Save(NexWorkspace workspace)
    {
        workspace = WorkspaceRules.Validate(workspace);
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO workspace_state(id,revision,definition) VALUES(1,$next,$json) ON CONFLICT(id) DO UPDATE SET revision=$next,definition=$json WHERE revision=$previous";
        var saved = workspace with { Revision = checked(workspace.Revision + 1) };
        command.Parameters.AddWithValue("$next", saved.Revision);
        command.Parameters.AddWithValue("$previous", workspace.Revision);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(saved));
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Workspace changed; reload before saving.");
        transaction.Commit(); return saved;
    }
}
