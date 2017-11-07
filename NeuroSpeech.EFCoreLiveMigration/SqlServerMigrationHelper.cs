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

                    columns.Add(col);
                }

            }
            return columns;
        }

        public async override Task SyncSchema(string name, List<SqlColumn> columns)
        {

            string createTable = $"IF NOT EXISTS (SELECT * FROM sysobjects WHERE Name='{name}' AND xtype='U')"
                + $" CREATE TABLE {name} ({ string.Join(",", columns.Where(x => x.IsPrimaryKey).Select(c => ToColumn(c))) })";

            using (var cmd = CreateCommand(createTable))
            {
                var n = await cmd.ExecuteNonQueryAsync();
            }

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

                    using (var cmd = CreateCommand($"EXEC sp_rename '{name}.{dest.ColumnName}', '{name}.{column.ColumnName}'")) {
                        await cmd.ExecuteNonQueryAsync();
                    }
                    dest.ColumnName = column.ColumnName;
                    
                }
                if (dest.Equals(column))
                    continue;


                columnsToAdd.Add(column);

                long m = DateTime.UtcNow.Ticks;

                column.CopyFrom = $"{dest.ColumnName}_{m}";

                using (var cmd = CreateCommand($"EXEC sp_rename '{name}.{dest.ColumnName}', '{dest.CopyFrom}'"))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

            }

            foreach (var column in columnsToAdd)
            {
                using (var cmd = CreateCommand($"ALTER TABLE {name} ADD " + ToColumn(column)))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            Console.WriteLine($"Table {name} sync complete");


            var copies = columns.Where(x => x.CopyFrom != null).ToList();

            if (copies.Any()) {
                foreach (var copy in copies)
                {
                    var update = $"UPDATE {name} SET {copy.ColumnName} = {copy.CopyFrom};";
                    using (var cmd = CreateCommand(update)) {
                        await cmd.ExecuteNonQueryAsync();
                    }
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
            }
            return name;
        }
    }
}
