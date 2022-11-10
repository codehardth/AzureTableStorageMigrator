using Azure.Data.Tables;

namespace Codehard.AzureTableStorageMigrator;

internal class MigrationOperation
{
    private MigrationOperation(
        OperationMode mode,
        string tableName,
        ITableEntity? entity)
    {
        this.Mode = mode;
        this.TableName = tableName;
        this.Entity = entity;
    }

    public static MigrationOperation Parse(string op)
    {
        // operation : ops_name table_name (partition_key row_key properties+) | (partition_key row_key) | *
        // ops_name : INS | UPD | DEL
        // table_name : text_and_number
        // partition_key : text_and_number
        // row_key : text_and_number
        // properties : text\|text\|text
        // text : [a-z_]+
        // text_and_number : [a-z0-9_]+
        var detail = op.Split(' ');

        var operation = detail[0];
        var tableName = detail[1];

        OperationMode mode;
        ITableEntity? entity = default;

        switch (operation)
        {
            case InternalConstants.DeleteMode:
            {
                var hasWildcard = detail.Contains("*");

                if (hasWildcard)
                {
                    mode = OperationMode.DeleteAll;
                }
                else
                {
                    mode = OperationMode.DeleteSingle;
                    var partitionKey = detail[2];
                    var rowKey = detail[3];
                    entity = GetTableEntity(partitionKey, rowKey);
                }

                break;
            }
            default:
            {
                mode = operation switch
                {
                    InternalConstants.InsertMode => OperationMode.Insert,
                    InternalConstants.UpdateMergeMode => OperationMode.UpdateMerge,
                    InternalConstants.UpdateReplaceMode => OperationMode.UpdateReplace,
                    InternalConstants.UpsertMergeMode => OperationMode.UpsertMerge,
                    InternalConstants.UpsertReplaceMode => OperationMode.UpsertReplace,
                    _ => throw new NotSupportedException($"Operation '{operation}' is not supported in this context"),
                };
                var partitionKey = detail[2];
                var rowKey = detail[3];
                entity = GetTableEntity(partitionKey, rowKey, detail[4..]);

                break;
            }
        }

        return new(mode, tableName, entity);
    }

    public OperationMode Mode { get; }

    public string TableName { get; }

    public ITableEntity? Entity { get; }

    private static TableEntity GetTableEntity(
        string partitionKey,
        string rowKey,
        params string[] properties)
    {
        var entity = new TableEntity
        {
            { "PartitionKey", partitionKey },
            { "RowKey", rowKey },
        };

        foreach (var row in properties)
        {
            var split = row.Split('|');

            var propertyName = split[0];
            var value = split[1];
            var type = split[2];

            var fieldValue = GetValue(value, type);

            if (fieldValue == null)
            {
                continue;
            }

            entity.Add(propertyName, fieldValue);
        }

        return entity;

        static object? GetValue(string value, string type)
        {
            return type switch
            {
                InternalConstants.StringType => value,
                InternalConstants.NumberType => value.Contains('.') ? double.Parse(value) : long.Parse(value),
                InternalConstants.BooleanType => value.ToLower() == "true",
                InternalConstants.NullType => null,
                _ => throw new NotSupportedException($"Type '{type}' is not supported in this context"),
            };
        }
    }
}