using InterfazInAction.Models;
using Microsoft.EntityFrameworkCore;

namespace InterfazInAction.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Aquí registramos nuestros modelos
        public DbSet<User> Users { get; set; }

        // Más adelante, aquí agregaremos:
        // public DbSet<IntegrationProcess> IntegrationProcesses { get; set; }
        // public DbSet<IntegrationField> IntegrationFields { get; set; }
    }
}