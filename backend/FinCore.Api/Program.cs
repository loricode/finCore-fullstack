using System.Text;
using System.Threading.RateLimiting;
using FinCore.Api.Authentication;
using FinCore.Api.Middlewares;
using FinCore.Application;
using FinCore.Application.Common.Abstractions;
using FinCore.Infrastructure;
using FinCore.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------ logging estructurado
builder.Services.AddSerilog((_, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Application", "FinCore.Api");
    if (builder.Configuration.GetValue("Logging:Json", true)) logger.WriteTo.Console(new CompactJsonFormatter());
    else logger.WriteTo.Console();
});

// ------------------------------------------------------------------ capas
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpRequestContext>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<HttpRequestContext>());
builder.Services.AddScoped<IRequestContext>(sp => sp.GetRequiredService<HttpRequestContext>());

// ------------------------------------------------------------------ autenticación y autorización
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // conserva "sub", "perm", "cid" tal cual
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
        };
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorizationBuilder()
    // Seguro por defecto: todo endpoint exige sesión salvo los marcados [AllowAnonymous].
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPerMinute", 20),
            Window = TimeSpan.FromMinutes(1),
        }));
});

// ------------------------------------------------------------------ errores, API y salud
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
{
    // Títulos en español para las respuestas que genera el framework (401/403 de autorización, 404, 429).
    ctx.ProblemDetails.Title = ctx.ProblemDetails.Status switch
    {
        StatusCodes.Status401Unauthorized when ctx.ProblemDetails.Title is null or "Unauthorized" => "No autenticado",
        StatusCodes.Status403Forbidden when ctx.ProblemDetails.Title is null or "Forbidden" => "Sin permiso",
        StatusCodes.Status404NotFound when ctx.ProblemDetails.Title is null or "Not Found" => "No encontrado",
        StatusCodes.Status429TooManyRequests => "Demasiadas solicitudes",
        _ => ctx.ProblemDetails.Title,
    };
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.Items[CorrelationIdMiddleware.ItemKey];
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "FinCore API";
    document.Info.Description = "Operaciones y conciliación financiera. Autenticación: Bearer JWT de POST /api/v1/auth/login.";
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    };
    return Task.CompletedTask;
}));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Detrás de nginx (contenedor web): la IP real llega en X-Forwarded-For.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// ------------------------------------------------------------------ pipeline
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnostics, http) =>
    diagnostics.Set("UserId", http.User.FindFirst("sub")?.Value));
app.UseExceptionHandler();
app.UseStatusCodePages(); // 401/403 sin cuerpo -> problem+json
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Configuration.GetValue("Api:ExposeDocs", app.Environment.IsDevelopment()))
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options => options.WithTitle("FinCore API")).AllowAnonymous();
}

app.MapControllers();

// live: el proceso responde (sin dependencias) · ready: además SQL Server responde
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

/// <summary>Visible para WebApplicationFactory en las pruebas de integración.</summary>
public partial class Program;
