using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace LiveMigrationConsole.Models
{
    public class ERPContext : DbContext
    {

        public ERPContext(DbContextOptions options):base(options)
        {

        }

        public DbSet<Product> Products { get; set; }

        public DbSet<Account> Accounts { get; set; }

    }
}
