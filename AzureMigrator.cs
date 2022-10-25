using Azure.Data.Tables;
using AzureTableStorageMigrator.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureTableStorageMigrator;

public class AzureMigrator : IMigrator
{
    private readonly MigrationOptions options;
    private readonly ILogger? logger;

    public AzureMigrator(
        MigrationOptions options,
        ILogger<AzureMigrator>? logger = default)
    {
        this.options = options;
        this.logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(this.options.Directory))
        {
            this.logger?.LogInformation("Migration directory does not exist");
            return;
        }

        var migrationFiles =
            Directory.EnumerateFiles(this.options.Directory, $"*{InternalConstants.MigrationFileExtension}")
                .OrderBy(f => f);

        if (!migrationFiles.Any())
        {
            this.logger?.LogInformation("No file found in migration directory");
            return;
        }

        var migrationHistoryTableClient =
            new TableClient(this.options.ConnectionString, InternalConstants.MigrationTableName);

        await migrationHistoryTableClient.CreateIfNotExistsAsync(cancellationToken);

        foreach (var file in migrationFiles)
        {
            var rowKey = Path.GetFileNameWithoutExtension(file);

            var history =
                await migrationHistoryTableClient.GetByPartitionKeyAndRowKeyAsync<MigrationHistory>(
                    InternalConstants.MigrationHistoryPartitionKey,
                    rowKey,
                    cancellationToken: cancellationToken);

            // Migration already applied.
            if (history != null)
            {
                continue;
            }

            try
            {
                var lines = await File.ReadAllLinesAsync(file, cancellationToken);

                var operations = lines.Select(l => new MigrationOperation(l));

                var batch = await this.BuildTableTransactionActionAsync(operations, cancellationToken);

                foreach (var chunk in batch.Chunk(100))
                {
                    await migrationHistoryTableClient.SubmitTransactionAsync(chunk, cancellationToken);
                }

                var migrationHistory = MigrationHistory.Create(rowKey);

                await migrationHistoryTableClient.AddEntityAsync(migrationHistory, cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger?.LogError(ex, "Unable to perform migration for {File}", file);
                throw;
            }
        }
    }

    private async ValueTask<IEnumerable<TableTransactionAction>> BuildTableTransactionActionAsync(
        IEnumerable<MigrationOperation> operations,
        CancellationToken cancellationToken = default)
    {
        var batch = new List<TableTransactionAction>();

        foreach (var op in operations)
        {
            switch (op.Mode)
            {
                case OperationMode.Insert:
                    batch.Add(new TableTransactionAction(TableTransactionActionType.Add, op.Entity));
                    break;
                case OperationMode.Update:
                    batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertMerge, op.Entity));
                    break;
                case OperationMode.DeleteSingle:
                    batch.Add(new TableTransactionAction(TableTransactionActionType.Delete, op.Entity));
                    break;
                case OperationMode.DeleteAll:
                    // Exceptional case that can't do transaction like the rest
                    var tableClient = new TableClient(this.options.ConnectionString, op.TableName);

                    await tableClient.DeleteAsync(cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Operation '{op.Mode}' is not supported in this context");
            }
        }

        return batch;
    }
}