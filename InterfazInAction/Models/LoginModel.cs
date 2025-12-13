using System.ComponentModel.DataAnnotations;

namespace InterfazInAction.Models
{
    public class LoginModel
    {
        // DataAnnotations ayudan a validar automáticamente en el Controller
        [Required(ErrorMessage = "El usuario es requerido.")]
        public string Usuario { get; set; } // O ClientId

        [Required(ErrorMessage = "La contraseña es requerida.")]
        public string Password { get; set; } // O ClientSecret

        /// <summary>
        /// (Opcional) Duración deseada del token en minutos. 
        /// Mínimo 1, Máximo 1440 (24 horas). Si se omite, se usa el default del servidor.
        /// </summary>
        [Range(1, 525600, ErrorMessage = "La duración debe estar entre 1 y 525600 minutos (1 año)")]
        public int? Duracion { get; set; }
    }
}
