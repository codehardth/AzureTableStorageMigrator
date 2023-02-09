namespace Codehard.AzureTableStorageMigrator;

public sealed record MigrationOptions(
    string ConnectionString,
    string Directory,
    string? ProjectName = default);