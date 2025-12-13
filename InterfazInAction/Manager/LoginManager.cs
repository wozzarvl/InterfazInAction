using InterfazInAction.Data;
using InterfazInAction.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace InterfazInAction.Manager
{
    public class LoginManager : ILoginManager
    {
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context; // Inyectamos el contexto de BD

        // "Base de datos" temporal de refresh tokens (Idealmente esto también debería ir a una tabla en Postgres)
        private static List<RefreshToken> UserRefreshTokens = new List<RefreshToken>();

        public LoginManager(IConfiguration configuration, AppDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public AuthResponseModel Login(LoginModel login)
        {
            // 1. Buscar el usuario en la base de datos (PostgreSQL)
            var user = _context.Users.FirstOrDefault(u => u.UserName == login.Usuario);

            // 2. Validar si el usuario existe
            if (user == null)
            {
                return null; // Usuario no encontrado
            }

            // 3. Validar la contraseña usando BCrypt
            // Compara la contraseña plana (login.Password) con el hash de la BD (user.PasswordHash)
            bool passwordValida = BCrypt.Net.BCrypt.Verify(login.Password, user.PasswordHash);
            //string hasheado = BCrypt.Net.BCrypt.HashPassword("l4l4In4ct10n");

            if (!passwordValida)
            {
                return null; // Contraseña incorrecta
            }

            // 4. Si llegamos aquí, las credenciales son válidas. Generamos los Claims.
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("rol", user.Role), // Usamos el rol que viene de la BD
                new Claim("id", user.Id.ToString())
            };

            int minutos = login.Duracion.HasValue ? login.Duracion.Value : 0;

            // 5. Generar Tokens
            var jwtToken = GenerateAccessToken(claims, minutos);
            var refreshToken = GenerateRefreshTokenString();

            // 6. Guardar Refresh Token (En memoria por ahora, pendiente mover a tabla DB)
            var refreshTokenEntity = new RefreshToken
            {
                Token = refreshToken,
                Usuario = user.UserName,
                Expires = DateTime.Now.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenDurationInDays"]))
            };
            UserRefreshTokens.Add(refreshTokenEntity);

            return new AuthResponseModel
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken),
                RefreshToken = refreshToken,
                Expiration = jwtToken.ValidTo.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        public AuthResponseModel RefreshToken(AuthResponseModel request)
        {
            // Lógica de Refresh Token (Sigue usando la lista en memoria por ahora)
            var storedToken = UserRefreshTokens.FirstOrDefault(x => x.Token == request.RefreshToken);

            if (storedToken == null || storedToken.Expires < DateTime.Now || storedToken.Revoked != null)
                return null;

            storedToken.Revoked = DateTime.Now;

            var newClaims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, storedToken.Usuario),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var newJwt = GenerateAccessToken(newClaims, 0);
            var newRefresh = GenerateRefreshTokenString();

            UserRefreshTokens.Add(new RefreshToken
            {
                Token = newRefresh,
                Usuario = storedToken.Usuario,
                Expires = DateTime.Now.AddDays(7)
            });

            return new AuthResponseModel
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(newJwt),
                RefreshToken = newRefresh,
                Expiration = newJwt.ValidTo.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        // Métodos privados auxiliares (Sin cambios mayores)
        private JwtSecurityToken GenerateAccessToken(IEnumerable<Claim> claims, int minutos)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var duration = minutos != 0
                ? DateTime.Now.AddMinutes(minutos)
                : DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:DurationInMinutes"]));

            return new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: duration,
                signingCredentials: creds
            );
        }

        private string GenerateRefreshTokenString()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public string Register(LoginModel login, string role)
        {
            // 1. Verificar si el usuario ya existe
            if (_context.Users.Any(u => u.UserName == login.Usuario))
            {
                return $"El usuario '{login.Usuario}' ya existe.";
            }

            // 2. Encriptar contraseña
            // Esto genera el hash seguro (ej. $2a$11$...) con sal automática.
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(login.Password);

            // 3. Crear entidad User
            var newUser = new User
            {
                UserName = login.Usuario,
                PasswordHash = passwordHash,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // 4. Guardar en BD
            _context.Users.Add(newUser);
            _context.SaveChanges();

            return "Usuario creado exitosamente.";
        }
    }
}