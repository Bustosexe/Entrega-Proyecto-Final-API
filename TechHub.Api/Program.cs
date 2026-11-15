using System.Reflection;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.Models;
using TechHub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuración local con secretos (contraseña de Gmail, etc.). No se sube a GitHub.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// QuestPDF: licencia Community (gratuita para proyectos educativos y empresas chicas).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// ---------------- Base de datos (EF Core Code-First + SQL Server) ----------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        // Las migraciones se escriben a mano y el snapshot puede quedar un paso atrás: no debe impedir que la API arranque.
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// ---------------- Seguridad: JWT ----------------
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);
var jwt = jwtSection.Get<JwtOptions>() ?? throw new InvalidOperationException("Falta la sección Jwt en appsettings.");
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres (256 bits) para HMAC-SHA256.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // conserva los nombres cortos: sub, name, role
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

// ---------------- Servicios de la aplicación ----------------
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IMovimientoService, MovimientoService>();
builder.Services.AddScoped<IImagenService, ImagenService>();
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<IComprobanteService, ComprobanteService>();
builder.Services.AddScoped<IReportesPdfService, ReportesPdfService>();
builder.Services.AddScoped<INotificacionesService, NotificacionesService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();

// Pagos por transferencia y emails (Gmail SMTP con contraseña de aplicación)
builder.Services.Configure<PagosOptions>(builder.Configuration.GetSection("Pagos"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton(Channel.CreateBounded<EmailMensaje>(new BoundedChannelOptions(200)
{
    FullMode = BoundedChannelFullMode.DropWrite
}));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<EmailBackgroundService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();
// El filtro de auditoría registra en la bitácora cada alta, modificación y baja.
builder.Services.AddControllers(o => o.Filters.Add<AuditoriaFilter>());

// ---------------- Swagger con botón "Authorize" para el Bearer Token ----------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TechHub API",
        Version = "v1",
        Description = "Sistema de gestión comercial de hardware y componentes de PC."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegá solo el token (sin la palabra Bearer). Se obtiene en POST /api/auth/login."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

// Crea/actualiza la base aplicando las migraciones y genera el admin inicial.
// Si la base no responde (por ejemplo, una cadena de conexión mal escrita en el hosting),
// la API igual arranca y el problema se puede ver en GET /api/estado, en vez de un error 500.30 sin detalle.
string? errorInicio = null;
try
{
    await DbSeeder.InicializarAsync(app.Services);
}
catch (Exception ex)
{
    errorInicio = ex.GetBaseException().Message;
    app.Logger.LogCritical(ex, "No se pudo inicializar la base de datos.");
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    // En desarrollo no se redirige a HTTPS para que el emulador de Android pueda usar http://10.0.2.2:5180
    app.UseHttpsRedirection();
}

if (app.Configuration.GetValue("Swagger:Habilitado", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TechHub API v1");
        c.DocumentTitle = "TechHub API - Documentación";
    });
}

// Imágenes de productos subidas (wwwroot/uploads)
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Estado del servicio: sirve para comprobar el despliegue (API + conexión a la base de datos).
app.MapGet("/api/estado", async (AppDbContext db) =>
{
    bool conecta;
    string? detalle = errorInicio;
    try
    {
        conecta = await db.Database.CanConnectAsync();
    }
    catch (Exception ex)
    {
        conecta = false;
        detalle ??= ex.GetBaseException().Message;
    }

    var ok = conecta && errorInicio is null;
    return Results.Json(new
    {
        api = "funcionando",
        entorno = app.Environment.EnvironmentName,
        baseDeDatos = ok ? "conectada" : "con problemas",
        detalle = ok ? null : detalle ?? "No se pudo conectar con la base de datos. Revisá la cadena de conexión."
    }, statusCode: ok ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}).WithTags("Estado").AllowAnonymous();

// La raíz del sitio muestra la documentación de la API.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();
