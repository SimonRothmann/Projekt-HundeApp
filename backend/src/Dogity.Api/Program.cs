using Dogity.Application;
using Dogity.Infrastructure;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// JSON-Antworten (Trainingslisten, GPS-Tracks mit vielen Punkten) sind gut
// kompressibel - spart auf mobilen Verbindungen auf dem Hundeplatz Bandbreite
// und Ladezeit bei vernachlässigbarem CPU-Overhead.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Dogity API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer Token: \"Bearer {token}\""
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Reihenfolge folgt ARCHITECTURE.md "Entwicklungsreihenfolge": Application
// vor Infrastructure, damit Infrastructure-Implementierungen die in
// Application definierten Abstraktionen erfüllen.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Hinter Caddy (Reverse Proxy im Docker-Netzwerk) sieht Kestrel als
// RemoteIpAddress nur die Proxy-IP. Für IP-basiertes Rate-Limiting und
// korrekte Logs die X-Forwarded-*-Header übernehmen. KnownNetworks/-Proxies
// leeren ist hier vertretbar: der Backend-Port ist nicht öffentlich
// exponiert (nur Caddy erreicht ihn über das interne Docker-Netzwerk),
// gespoofte Header von außen kommen also gar nicht erst an.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// IP-basiertes Rate-Limit für die anonymen Auth-Endpoints (Login, Register,
// Forgot-Password): Identity-Lockout schützt nur pro Konto - ohne Drosselung
// pro IP bleiben Passwort-Spraying über viele Konten, Massenregistrierung
// und E-Mail-Versand-Spam über forgot-password möglich. 10 Requests/Minute
// pro IP reicht für jede legitime Nutzung (auch Familien hinter einem NAT).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Bewusst permissiv in Development: erlaubt z.B. das Testen von
            // einem Smartphone im selben WLAN über die LAN-IP des Rechners,
            // ohne diese (dynamische, oft per DHCP wechselnde) IP fest in
            // Cors:AllowedOrigins eintragen zu müssen. In Production bleibt
            // die feste Origin-Liste aus der Konfiguration Pflicht.
            policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// Migrationen, Rollen, Sportarten-/Übungskatalog und Admin-Bootstrap laufen
// IMMER beim Start, nicht nur in Development - das sind keine Demo-/
// Testdaten, sondern die Daten, die die App auch in Production für ihre
// Kernfunktion (Trainingspläne gegen echte Prüfungsordnungen) zwingend
// braucht. Ohne das würde eine Production-Instanz mit leerem Sportarten-
// katalog starten. Swagger-UI und die erfundenen Demo-Accounts/-Daten
// bleiben dagegen bewusst auf Development beschränkt.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    await RoleSeeder.SeedAsync(scope.ServiceProvider);
    await SportCatalogSeeder.SeedAsync(scope.ServiceProvider);
    await AdminBootstrapper.SeedAsync(scope.ServiceProvider, builder.Configuration);

    if (app.Environment.IsDevelopment())
        await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Muss vor allem laufen, was die Client-IP liest (Rate-Limiter, Logging).
app.UseForwardedHeaders();

app.UseResponseCompression();

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");

app.MapControllers();

app.Run();
