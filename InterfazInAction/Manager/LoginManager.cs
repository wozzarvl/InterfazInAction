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
        private readonly AppDbContext _context; 

        
        //private static List<RefreshToken> UserRefreshTokens = new List<RefreshToken>();

        public LoginManager(IConfiguration configuration, AppDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public AuthResponseModel Login(LoginModel login)
        {
            
            var user = _context.users.FirstOrDefault(u => u.UserName == login.Usuario);

            
            if (user == null)
            {
                return null; 
            }

            
            bool passwordValida = BCrypt.Net.BCrypt.Verify(login.Password, user.PasswordHash);
            //string hasheado = BCrypt.Net.BCrypt.HashPassword("l4l4In4ct10n");

            if (!passwordValida)
            {
                return null; 
            }

            
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("rol", user.Role),
                new Claim("id", user.Id.ToString())
            };

            int minutos = login.Duracion.HasValue ? login.Duracion.Value : 0;

            
            var jwtToken = GenerateAccessToken(claims, minutos);
            var refreshTokenStr = GenerateRefreshTokenString();

            // 6. Guardar Refresh Token (En memoria por ahora, pendiente mover a tabla DB)
            var refreshTokenEntity = new RefreshToken
            {
                Token = refreshTokenStr,
                Usuario = user.UserName,
                Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenDurationInDays"])),
                Created = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            _context.SaveChanges(); // Guardamos en Postgres

            return new AuthResponseModel
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken),
                RefreshToken = refreshTokenStr,
                Expiration = jwtToken.ValidTo.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        public AuthResponseModel RefreshToken(AuthResponseModel request)
        {
            
            var storedToken = _context.RefreshTokens
                .FirstOrDefault(x => x.Token == request.RefreshToken);

            
            if (storedToken == null || storedToken.Expires < DateTime.UtcNow || storedToken.Revoked != null)
                return null;

            // 4. REVOCAR EL TOKEN ANTERIOR 
            storedToken.Revoked = DateTime.UtcNow;
            _context.RefreshTokens.Update(storedToken);
            _context.SaveChanges();

            
            var newClaims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, storedToken.Usuario),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var newJwt = GenerateAccessToken(newClaims, 0);
            var newRefreshStr = GenerateRefreshTokenString();

         
            var newTokenEntity = new RefreshToken
            {
                Token = newRefreshStr,
                Usuario = storedToken.Usuario,
                Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenDurationInDays"])), // Usar config
                Created = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(newTokenEntity);
            _context.SaveChanges();

            return new AuthResponseModel
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(newJwt),
                RefreshToken = newRefreshStr,
                Expiration = newJwt.ValidTo.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

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
            if (_context.users.Any(u => u.UserName == login.Usuario))
            {
                return $"El usuario '{login.Usuario}' ya existe.";
            }

         
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(login.Password);

            // 3. Crear entidad User
            var newUser = new user
            {
                UserName = login.Usuario,
                PasswordHash = passwordHash,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // 4. Guardar en BD
            _context.users.Add(newUser);
            _context.SaveChanges();

            return "Usuario creado exitosamente.";
        }
    }
}