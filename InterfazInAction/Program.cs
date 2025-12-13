using InterfazInAction.Manager;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using InterfazInAction.Data;
using Microsoft.EntityFrameworkCore;



var builder = WebApplication.CreateBuilder(args);


var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings.GetValue<string>("Key");
var issuer = jwtSettings.GetValue<string>("Issuer");
var audience = jwtSettings.GetValue<string>("Audience");

var keyBytes = Encoding.UTF8.GetBytes(secretKey!);

// ---------------------------------------------------------
//  Configuración de Base de Datos (PostgreSQL)
// ---------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
//---------------------------------------------------------

// Add services to the container.

builder.Services.AddAuthentication(config => {
    config.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    config.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(config => {
    config.RequireHttpsMetadata = true; // Pon en true para producción
    config.SaveToken = true;
    config.TokenValidationParameters = new TokenValidationParameters
            {
                // Validar que el token fue firmado por nosotros
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

                // Validar quién emitió el token (opcional pero recomendado)
                ValidateIssuer = true,
                ValidIssuer = issuer,

                // Validar para quién es el token (opcional pero recomendado)
                ValidateAudience = true,
                ValidAudience = audience,

                

                // Importante: Evita el margen de error de tiempo (por defecto es 5 min)
                // Si el token expira, expira YA.
                ClockSkew = TimeSpan.Zero
            };
        });


builder.Services.AddScoped<ILoginManager, LoginManager>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
