using InterfazInAction.Models;
using Microsoft.EntityFrameworkCore;

namespace InterfazInAction.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

       
        public DbSet<user> users { get; set; }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<integrationProcess> integrationProcesses { get; set; }
        public DbSet<integrationField> integrationFields { get; set; }
    }
}