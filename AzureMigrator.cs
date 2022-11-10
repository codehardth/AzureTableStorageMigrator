using Azure.Data.Tables;
using Codehard.AzureTableStorageMigrator.Extensions;
using Microsoft.Extensions.Logging;

namespace Codehard.AzureTableStorageMigrator;

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

                var operations = lines.Select(MigrationOperation.Parse);

                await this.ApplyMigrationOperationsAsync(operations, cancellationToken);

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

    private async ValueTask ApplyMigrationOperationsAsync(
        IEnumerable<MigrationOperation> operations,
        CancellationToken cancellationToken = default)
    {
        foreach (var group in operations.GroupBy(o => o.TableName))
        {
            var tableName = group.Key;

            var tableClient = new TableClient(this.options.ConnectionString, tableName);

            await tableClient.CreateIfNotExistsAsync(cancellationToken);

            var batch = new List<TableTransactionAction>();

            foreach (var op in group)
            {
                switch (op.Mode)
                {
                    case OperationMode.DeleteAll:
                        // Exceptional case that can't do transaction like the rest
                        await tableClient.DeleteAsync(cancellationToken);

                        break;
                    case OperationMode.UpdateAll:
                    {
                        await foreach (var tx in PrepareUpdatedEntitiesTransactionActionAsync(tableClient, op))
                        {
                            batch.Add(tx);
                        }

                        break;
                    }
                    default:
                        batch.Add(
                            new TableTransactionAction(
                                op.Mode switch
                                {
                                    OperationMode.Insert => TableTransactionActionType.Add,
                                    OperationMode.UpdateMerge => TableTransactionActionType.UpdateMerge,
                                    OperationMode.UpdateReplace => TableTransactionActionType.UpdateReplace,
                                    OperationMode.UpsertMerge => TableTransactionActionType.UpsertMerge,
                                    OperationMode.UpsertReplace => TableTransactionActionType.UpsertReplace,
                                    OperationMode.DeleteSingle => TableTransactionActionType.Delete,
                                    _ => throw new NotSupportedException(
                                        $"Operation '{op.Mode}' is not supported in this context"),
                                }, op.Entity));

                        break;
                }
            }

            foreach (var partition in batch.GroupBy(e => e.Entity.PartitionKey))
            {
                foreach (var chunk in partition.Chunk(100))
                {
                    await tableClient.SubmitTransactionAsync(chunk, cancellationToken);
                }
            }
        }

        async IAsyncEnumerable<TableTransactionAction> PrepareUpdatedEntitiesTransactionActionAsync(
            TableClient tableClient,
            MigrationOperation op)
        {
            const int pageSize = 100;

            var pages =
                tableClient.QueryAsync<TableEntity>(maxPerPage: pageSize,
                    cancellationToken: cancellationToken);

            await foreach (var page in pages.AsPages(pageSizeHint: pageSize)
                               .WithCancellation(cancellationToken))
            {
                var entities = page.Values;

                foreach (var entity in entities)
                {
                    var updatedEntity = op.MutateEntityFunc?.Invoke(entity);

                    if (updatedEntity != null)
                    {
                        yield return new TableTransactionAction(TableTransactionActionType.UpdateMerge, updatedEntity);
                    }
                }
            }
        }
    }
}