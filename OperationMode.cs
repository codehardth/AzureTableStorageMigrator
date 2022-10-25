namespace AzureTableStorageMigrator;

internal enum OperationMode
{
    Insert,
    Update,
    UpsertMerge,
    UpsertReplace,
    DeleteSingle,
    DeleteAll,
}