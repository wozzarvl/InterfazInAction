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
                
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

               
                ValidateIssuer = true,
                ValidIssuer = issuer,

                
                ValidateAudience = true,
                ValidAudience = audience,

                

               
                ClockSkew = TimeSpan.Zero
            };
        });


builder.Services.AddScoped<ILoginManager, LoginManager>();
builder.Services.AddScoped<IDynamicXmlManager, DynamicXmlManager>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();



// --- INICIO DEL BLOQUE DE AUTO-DEPLOY ---
// Creamos un scope temporal para obtener los servicios
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        
        var context = services.GetRequiredService<AppDbContext>();

        //  Migraciones + Seed
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al inicializar la base de datos.");
    }
}
// --- FIN DEL BLOQUE DE AUTO-DEPLOY ---


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
