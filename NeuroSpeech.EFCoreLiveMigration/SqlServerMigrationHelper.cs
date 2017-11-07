using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace NeuroSpeech.EFCoreLiveMigration
{
    internal class SqlServerMigrationHelper : MigrationHelper
    {
        public SqlServerMigrationHelper(DbContext context) : base(context)
        {
        }

        public override DbCommand CreateCommand(string command, Dictionary<string, object> plist = null)
        {
            var cmd = context.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = command;
            cmd.Transaction = Transaction;
            if (plist != null)
            {
                foreach (var p in plist)
                {
                    var px = cmd.CreateParameter();
                    px.ParameterName = p.Key;
                    px.Value = p.Value;                    
                    cmd.Parameters.Add(px);
                }
            }
            return cmd;
        }

        public override async Task<List<SqlColumn>> GetCommonSchemaAsync(string name)
        {


            List<SqlColumn> columns = new List<SqlColumn>();
            string sqlColumns = Scripts.SqlServerGetSchema;

            using (var reader = await ReadAsync(sqlColumns, new Dictionary<string, object> { { "@TableName", name } }))
            {


                while (await reader.ReadAsync())
                {
                    SqlColumn col = new SqlColumn();

                    col.ColumnName = reader.GetValue<string>("ColumnName");
                    col.IsPrimaryKey = reader.GetValue<bool>("IsPrimaryKey");
                    col.IsNullable = reader.GetValue<string>("IsNullable") == "YES";
                    col.ColumnDefault = reader.GetValue<string>("ColumnDefault");
                    col.DataType = reader.GetValue<string>("DataType");
                    col.DataLength = reader.GetValue<int>("DataLength");
                    col.NumericPrecision = reader.GetValue<decimal?>("NumericPrecision");
                    col.NumericScale = reader.GetValue<decimal?>("NumericScale");
                    col.IsIdentity = reader.GetValue<bool>("IsIdentity");

                    columns.Add(col);
                }

            }
            return columns;
        }

        public async override Task SyncSchema(string schema, string name, List<SqlColumn> columns)
        {

            schema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema;
            
            string createTable = $"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='{name}' AND TABLE_SCHEMA = '{schema}')"
                + $" CREATE TABLE {schema}.{name} ({ string.Join(",", columns.Where(x => x.IsPrimaryKey).Select(c => ToColumn(c))) })";

            await RunAsync(createTable);

            var destColumns = await GetCommonSchemaAsync(name);

            List<SqlColumn> columnsToAdd = new List<SqlColumn>();

            foreach (var column in columns)
            {
                var dest = destColumns.FirstOrDefault(x => x.ColumnName == column.ColumnName);
                if (dest == null)
                {

                    // look for old names....
                    dest = destColumns.FirstOrDefault(x => column.OldNames != null && 
                        column.OldNames.Any( oc => oc.EqualsIgnoreCase(x.ColumnName) ));

                    if (dest == null)
                    {


                        columnsToAdd.Add(column);
                        continue;
                    }

                    await RunAsync($"EXEC sp_rename '{name}.{dest.ColumnName}', '{column.ColumnName}'");
                    dest.ColumnName = column.ColumnName;
                    
                }
                if (dest.Equals(column))
                    continue;


                columnsToAdd.Add(column);

                long m = DateTime.UtcNow.Ticks;

                column.CopyFrom = $"{dest.ColumnName}_{m}";

                await RunAsync($"EXEC sp_rename '{name}.{dest.ColumnName}', '{column.CopyFrom}'");

            }

            foreach (var column in columnsToAdd)
            {
                await RunAsync($"ALTER TABLE {name} ADD " + ToColumn(column));
            }

            Console.WriteLine($"Table {name} sync complete");


            var copies = columns.Where(x => x.CopyFrom != null).ToList();

            if (copies.Any()) {
                foreach (var copy in copies)
                {
                    var update = $"UPDATE {name} SET {copy.ColumnName} = {copy.CopyFrom};";
                    await RunAsync(update);
                }
            }
        }

        private static string[] textTypes = new[] { "nvarchar", "varchar" };

        private static bool IsText(string n) => textTypes.Any(a => a.Equals(n, StringComparison.OrdinalIgnoreCase));

        private static bool IsDecimal(string n) => n.Equals("decimal", StringComparison.OrdinalIgnoreCase);

        private string ToColumn(SqlColumn c)
        {
            var name = $"{c.ColumnName} {c.DataType}";
            if (IsText(c.DataType))
            {
                if (c.DataLength > 0 && c.DataLength < int.MaxValue)
                {
                    name += $"({ c.DataLength })";
                }
                else
                {
                    name += "(MAX)";
                }
            }
            if (IsDecimal(c.DataType))
            {
                name += $"({ c.NumericPrecision },{ c.NumericScale })";
            }
            if (!c.IsPrimaryKey)
            {
                // lets allow nullable to every field...
                name += " NULL ";
            }
            else
            {
                name += " PRIMARY KEY ";
                if (c.IsIdentity) {
                    name += " Identity ";
                }
            }
            return name;
        }

        internal override async Task SyncIndexes(string schema, string tableName, IEnumerable<IIndex> indexes)
        {

            var destIndexes = await GetIndexesAsync(tableName);

            foreach (var index in indexes) {
                var name = index.Relational().Name;

                var columns = index.Properties.Select(p => p.Relational().ColumnName).ToArray();

                var newColumns = columns.ToJoinString();

                var existing = destIndexes.FirstOrDefault(x => x.Name == name);
                if (existing != null) {
                    // see if all are ok...
                    var existingColumns = existing.Columns.ToJoinString();

                    if (existingColumns.EqualsIgnoreCase(newColumns))
                        continue;

                    // rename old index... 
                    var n = $"{name}_{System.DateTime.UtcNow.Ticks}";

                    await RunAsync($"EXEC sp_rename @FromName, @ToName, @Type", new Dictionary<string, object> {
                        { "@FromName", tableName + "." + name },
                        { "@ToName", n},
                        { "@Type", "INDEX" }
                    });
                }

                // lets create index...

                await RunAsync($"CREATE NONCLUSTERED INDEX {name} ON {tableName} ({ newColumns })");
            }
        }

        public override async Task<List<SqlIndex>> GetIndexesAsync(string name)
        {
            List<SqlIndex> list = new List<SqlIndex>();
            using (var reader = await ReadAsync(Scripts.SqlServerGetIndexes, new Dictionary<string, object> {
                { "@TableName", name }
            })) {

                while (await reader.ReadAsync()) {
                    var index = new SqlIndex();

                    index.Name = reader.GetValue<string>("IndexName");
                    index.Columns = new string[] {
                        reader.GetValue<string>("ColumnName")
                    };

                    list.Add(index);
                }

                list = list.GroupBy(x => x.Name).Select(x => new SqlIndex {
                    Name = x.Key,
                    Columns = x.SelectMany( c => c.Columns ).ToArray()
                }).ToList();

            }
            return list;
        }
    }
}
