using Azure;
using Azure.Data.Tables;

namespace Codehard.AzureTableStorageMigrator;

public class MigrationHistory : ITableEntity
{
    public string PartitionKey { get; set; } = null!;

    public string RowKey { get; set; } = null!;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public static MigrationHistory Create(string migrationName)
    {
        return new MigrationHistory
        {
            PartitionKey = "migration",
            RowKey = migrationName,
        };
    }
}