using CanisTrack.Application;
using CanisTrack.Infrastructure;
using CanisTrack.Infrastructure.Identity;
using CanisTrack.Infrastructure.Persistence;
using CanisTrack.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "CanisTrack API", Version = "v1" });
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    await RoleSeeder.SeedAsync(scope.ServiceProvider);
    await SportCatalogSeeder.SeedAsync(scope.ServiceProvider);
    await AdminBootstrapper.SeedAsync(scope.ServiceProvider, builder.Configuration);
    await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");

app.MapControllers();

app.Run();
