using Azure;
using Azure.Data.Tables;

namespace Codehard.AzureTableStorageMigrator.Extensions;

public static class TableClientExtensions
{
    public static async Task<T?> GetByPartitionKeyAndRowKeyAsync<T>(
        this TableClient tableClient,
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        try
        {
            var entity =
                await tableClient.GetEntityAsync<T>(partitionKey, rowKey, cancellationToken: cancellationToken);

            return entity?.Value;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }
}