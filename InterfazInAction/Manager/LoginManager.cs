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

        // "Base de datos" temporal de refresh tokens (Deberías usar tu DBContext aquí en el futuro)
        private static List<RefreshToken> UserRefreshTokens = new List<RefreshToken>();

        // AQUI ESTÁ LA MAGIA: Inyección por constructor
        public LoginManager(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public AuthResponseModel Login(LoginModel login)
        {
            // 1. Validar credenciales
            bool credencialesValidas = (login.Usuario == "admin" && login.Password == "1234");
            if (!credencialesValidas) return null; // Retornamos null si falla

            // 2. Crear Claims
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, login.Usuario),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("rol", "Administrador"),
           // new Claim("Caduca",DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:DurationInMinutes"])).ToString("yyyy-MM-dd HH:mm:ss"))

        };

            int minutos = login.Duracion.HasValue ? login.Duracion.Value : 0;

            // 3. Generar Tokens
            var jwtToken = GenerateAccessToken(claims,minutos);
            var refreshToken = GenerateRefreshTokenString();

            

            // 4. Guardar Refresh Token
            var refreshTokenEntity = new RefreshToken
            {
                Token = refreshToken,
                Usuario = login.Usuario,
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
            // Lógica de Refresh Token (Mover lo que hicimos antes aquí)
            var storedToken = UserRefreshTokens.FirstOrDefault(x => x.Token == request.RefreshToken);

            // Validaciones...
            if (storedToken == null || storedToken.Expires < DateTime.Now || storedToken.Revoked != null)
                return null;

            storedToken.Revoked = DateTime.Now; // Revocar anterior

            // Generar nuevos...
            var newClaims = new[] {
            new Claim(JwtRegisteredClaimNames.Sub, storedToken.Usuario),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            var newJwt = GenerateAccessToken(newClaims,0);
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

        // Métodos privados auxiliares
        private JwtSecurityToken GenerateAccessToken(IEnumerable<Claim> claims,int minutos)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            return new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,

                expires: minutos!=0?DateTime.Now.AddMinutes( minutos):  DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:DurationInMinutes"])),
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
    }
}
