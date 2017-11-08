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

        public DbTransaction Transaction { get; private set; }

        public MigrationHelper(DbContext context)
        {
            this.context = context;
        }

        public static MigrationHelper ForSqlServer(DbContext context) {
            return new SqlServerMigrationHelper(context);
        }

     

        public async Task MigrateAsync() {

            await context.Database.EnsureCreatedAsync();

            foreach (var entity in context.Model.GetEntityTypes())
            {
                var relational = entity.Relational();

                var columns = entity.GetProperties().Select(x => CreateColumn(x)).ToList();

                var indexes = entity.GetIndexes();

                var fkeys = entity.GetForeignKeys();
                

                try
                {
                        
                    context.Database.OpenConnection();
                    using (var tx = context.Database.GetDbConnection().BeginTransaction(System.Data.IsolationLevel.Serializable))
                    {
                        this.Transaction = tx;
                        await SyncSchema(relational.Schema, relational.TableName, columns);

                        await SyncIndexes(relational.Schema, relational.TableName, indexes);

                        await SyncIndexes(relational.Schema, relational.TableName, fkeys);

                        tx.Commit();
                    }
                }
                finally {
                    context.Database.CloseConnection();
                }
            }

            

        }

        internal abstract Task SyncIndexes(string schema, string tableName, IEnumerable<IForeignKey> fkeys);
        internal abstract Task SyncIndexes(string schema, string tableName, IEnumerable<IIndex> indexes);

        private static SqlColumn CreateColumn(IProperty x)
        {
            var r = new SqlColumn
            {
                CLRType = x.ClrType,
                ColumnDefault = x.Relational().DefaultValueSql,
                ColumnName = x.Relational().ColumnName,
                DataLength = x.GetMaxLength() ?? 0,
                DataType = x.GetColumnType(),
                IsNullable = x.IsColumnNullable(),
                IsPrimaryKey = x.IsPrimaryKey()
            };

            var rr = x.Relational();
            

            r.IsIdentity = x.ValueGenerated == ValueGenerated.OnAdd;

            r.OldNames = x.GetOldNames();

            return r;
        }

        public abstract DbCommand CreateCommand(String command, Dictionary<string, object> plist = null);

        public async Task<int> RunAsync(string command, Dictionary<string, object> plist = null) {
            using (var cmd = CreateCommand(command, plist)) {
                try
                {
                    return await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex) {
                    throw new InvalidOperationException($"RunAsync failed for {command}", ex);
                }
            }
        }

        public async Task<SqlRowSet> ReadAsync(string command, Dictionary<string, object> plist)
        {
            var cmd = CreateCommand(command, plist);
            return new SqlRowSet(cmd, await cmd.ExecuteReaderAsync());
        }

        public abstract Task<List<SqlColumn>> GetCommonSchemaAsync(string name);
        public abstract Task SyncSchema(string schema, string table, List<SqlColumn> schemaTable);

        public abstract Task<List<SqlIndex>> GetIndexesAsync(string name);


    }
}
