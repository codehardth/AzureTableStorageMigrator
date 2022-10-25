namespace Codehard.AzureTableStorageMigrator;

internal enum OperationMode
{
    Insert,
    UpdateMerge,
    UpdateReplace,
    UpsertMerge,
    UpsertReplace,
    DeleteSingle,
    DeleteAll,
}