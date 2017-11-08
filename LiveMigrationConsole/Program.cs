using LiveMigrationConsole.Models;
using Microsoft.EntityFrameworkCore;
using NeuroSpeech.EFCoreLiveMigration;
using System;

namespace LiveMigrationConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            DbContextOptionsBuilder<ERPContext> options = new DbContextOptionsBuilder<ERPContext>();
            options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ERPModel;Trusted_Connection=True;MultipleActiveResultSets=true");

            using (var db = new ERPContext(options.Options)) {

                MigrationHelper.ForSqlServer(db).Migrate();
                
            }

            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }
    }
}
