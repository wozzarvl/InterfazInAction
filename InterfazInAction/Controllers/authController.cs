using InterfazInAction.Manager;
using InterfazInAction.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InterfazInAction.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase // Clase con Mayúscula inicial
    {
        private readonly ILoginManager _loginManager; // Ya no inyectamos IConfiguration aquí

        // Inyectamos el Manager
        public AuthController(ILoginManager loginManager)
        {
            _loginManager = loginManager;
        }



        /// <summary>
        /// Inicia sesión y genera los tokens de acceso.
        /// </summary>
        /// <remarks>
        /// Envía las credenciales del usuario (o ClientId/Secret) para obtener un AccessToken de corta duración 
        /// y un RefreshToken para renovar la sesión posteriormente.
        /// </remarks>
        /// <param name="login">Objeto con usuario y contraseña.</param>
        /// <returns>Un objeto con el token, refresh token y fecha de expiración.</returns>
        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponseModel), StatusCodes.Status200OK)] 
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public IActionResult Login([FromBody] LoginModel login)
        {
            var resultado =   _loginManager.Login(login);

            if (resultado == null)
            {
                return Unauthorized(new { message = "Credenciales inválidas" });
            }

            return Ok(resultado);
        }

        [AllowAnonymous]
        [HttpPost("refreshToken")]
        [ProducesResponseType(typeof(AuthResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public IActionResult Refresh([FromBody] AuthResponseModel request)
        {
            var resultado = _loginManager.RefreshToken(request);

            if (resultado == null)
            {
                return BadRequest(new { message = "Token inválido o expirado" });
            }

            return Ok(resultado);
        }


        [HttpPost("register")]
        [AllowAnonymous] // Permitimos registrar el primer usuario sin estar logueados
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public IActionResult Register([FromBody] LoginModel login, [FromQuery] string role = "administrador")
        {
            // Llamamos al manager
            var resultado = _loginManager.Register(login, role);

            if (resultado.Contains("ya existe"))
            {
                return BadRequest(new { message = resultado });
            }

            return Ok(new { message = resultado });
        }


        [Authorize]
        [HttpGet("prueba")]
        public IActionResult prueba()
        {
            return Ok();
        }
    }
}