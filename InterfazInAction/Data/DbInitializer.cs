using InterfazInAction.Models;
using Microsoft.EntityFrameworkCore;

namespace InterfazInAction.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
         
            try
            {
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                
                Console.WriteLine($"--> Error al aplicar migraciones: {ex.Message}");
                throw; 
            }

          
            if (context.Users.Any())
            {
                return;   // La DB ya tiene datos
            }

            Console.WriteLine("--> Sembrando base de datos con usuario Admin...");

            // Crear el usuario Administrador por defecto
            var adminUser = new User
            {
                UserName = "admin",
                // Importante: Usamos BCrypt para hashear la contraseña "l4l4In4ct10n"
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("l4l4In4ct10n"),
                Role = "Administrador",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(adminUser);


            context.SaveChanges();
            Console.WriteLine("--> Base de datos sembrada exitosamente.");
        }
    }
}