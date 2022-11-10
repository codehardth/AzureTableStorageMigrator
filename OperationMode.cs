namespace Codehard.AzureTableStorageMigrator;

internal enum OperationMode
{
    Insert,
    UpdateMerge,
    UpdateReplace,
    UpdateAll,
    UpsertMerge,
    UpsertReplace,
    DeleteSingle,
    DeleteAll,
}