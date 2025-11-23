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
    }
}
