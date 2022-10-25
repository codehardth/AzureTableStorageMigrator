# AzureTableStorageMigrator

A migration library for Azure Table Storage.

### How to use
This library will apply migrations with file extension `.am` (Azure Migration) using following syntax grammar

```
operation : ops_name table_name (partition_key row_key properties+) | (partition_key row_key) | *
ops_name : INSERT | UPDATEM | UPDATER | DELETE | UPSERTM | UPSERTR
table_name : text_and_number
partition_key : text_and_number
row_key : text_and_number
properties : text\|text\|text
text : [a-z_]+
text_and_number : [a-z0-9_]+
```

Simply instantiate `AzureMigrator` object with migration directory, then run `MigrateAsync`