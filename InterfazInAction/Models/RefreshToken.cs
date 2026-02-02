using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InterfazInAction.Models
{
    // Cambiar a public para que el Context lo vea
    public class RefreshToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Token { get; set; }
        public string Usuario { get; set; } // Podrías relacionarlo con user Id, pero por nombre funciona
        public DateTime Expires { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow; // Mejor usar UtcNow en DB
        public DateTime? Revoked { get; set; }

        // Esta propiedad no se mapea a base de datos, es lógica
        [NotMapped]
        public bool IsActive => Revoked == null && DateTime.UtcNow < Expires;
    }
}