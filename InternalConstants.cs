namespace AzureTableStorageMigrator;

internal static class InternalConstants
{
    public const string MigrationTableName = "MigrationHistories";

    public const string MigrationHistoryPartitionKey = "migration";

    public const string MigrationFileExtension = ".am";

    public const string InsertMode = "INSERT";
    public const string UpdateMergeMode = "UPDATEM";
    public const string UpdateReplaceMode = "UPDATER";
    public const string DeleteMode = "DELETE";
    public const string UpsertMergeMode = "UPSERTM";
    public const string UpsertReplaceMode = "UPSERTR";

    public const string StringType = "STRING";
    public const string NumberType = "NUMBER";
    public const string BooleanType = "BOOL";
    public const string NullType = "NULL";
}