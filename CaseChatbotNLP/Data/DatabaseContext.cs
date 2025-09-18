using Microsoft.EntityFrameworkCore;
using CaseChatbotNLP.Data.Entities;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace CaseChatbotNLP.Data
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }
        public DbSet<Caso> Casos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Caso>().HasIndex(c => c.Sede);
            modelBuilder.Entity<Caso>().HasIndex(c => c.Responsable);
            modelBuilder.Entity<Caso>().HasIndex(c => c.Estado);
        }
    }
}