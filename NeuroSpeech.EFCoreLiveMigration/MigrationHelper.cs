using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data.Common;
using System.Threading.Tasks;
using System.Transactions;
using System.Linq;

namespace NeuroSpeech.EFCoreLiveMigration
{
    public abstract class MigrationHelper
    {
        protected readonly DbContext context;

        public MigrationHelper(DbContext context)
        {
            this.context = context;
        }

        public static MigrationHelper ForSqlServer(DbContext context) {
            return new SqlServerMigrationHelper(context);
        }

     

        public async Task MigrateAsync() {

            foreach (var entity in context.Model.GetEntityTypes()) {
                var relational = entity.Relational();

                var name = $"[{relational.Schema}].[{relational.TableName}]";

                var columns = entity.GetProperties().Select(x=> new SqlColumn {
                     CLRType = x.ClrType,
                     ColumnDefault = x.Relational().DefaultValueSql,
                      ColumnName = x.Relational().ColumnName,
                      DataLength = x.GetMaxLength() ?? 0,
                       DataType = x.Relational().ColumnType,
                        IsNullable = x.IsColumnNullable(),
                         IsPrimaryKey = x.IsPrimaryKey()
                }).ToList();

                await SyncSchema(name, columns);
            }

        }

        public abstract DbCommand CreateCommand(String command, Dictionary<string, object> plist = null);

        public async Task<SqlRowSet> ReadAsync(string command, Dictionary<string, object> plist)
        {
            var cmd = CreateCommand(command, plist);
            return new SqlRowSet(cmd, await cmd.ExecuteReaderAsync());
        }

        public abstract Task<List<SqlColumn>> GetCommonSchemaAsync(string name);
        public abstract Task SyncSchema(string name, List<SqlColumn> schemaTable);


    }
}
