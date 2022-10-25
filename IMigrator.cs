namespace AzureTableStorageMigrator;

public interface IMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}