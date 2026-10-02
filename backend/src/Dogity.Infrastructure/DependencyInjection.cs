using Dogity.Application.Abstractions;
using Dogity.Infrastructure.Email;
using Dogity.Infrastructure.Geocoding;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Weather;
using Dogity.Infrastructure.Import;
using Dogity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Dogity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;

                // Brute-Force-Schutz: nach 5 falschen Passwörtern in Folge
                // wird das Konto für 5 Minuten automatisch gesperrt (siehe
                // AuthController.Login - CheckPasswordSignInAsync stößt das
                // an). Das sind die ASP.NET-Identity-Standardwerte, hier
                // bewusst explizit gesetzt statt implizit zu verlassen.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddErrorDescriber<DeutscherIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddMemoryCache();
        services.AddScoped<KontoStatus>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IRegulationPdfParser, RegulationPdfParser>();

        // Typisierter HttpClient für Open-Meteo (kostenlos, ohne API-Key).
        // Kurzer Timeout: Wetter ist eine Anreicherung - lieber ohne Wert
        // speichern als den Nutzer warten lassen (der Provider gibt bei
        // Zeitüberschreitung null zurück, siehe OpenMeteoWeatherProvider).
        services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(6);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Dogity/1.0 (Hundesport-Trainingstagebuch)");
        });

        // Ortssuche über Photon (OpenStreetMap) - getrennt vom Wetter, weil es
        // ein anderer Dienst ist und Hundeplätze nur in OSM zu finden sind.
        // Noch kürzerer Timeout: das läuft während des Tippens.
        services.AddHttpClient<IGeocodingProvider, PhotonGeocodingProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(4);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Dogity/1.0 (Hundesport-Trainingstagebuch)");
        });

        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        // LoggingEmailSender ist aktiv, bis echte SMTP-Zugangsdaten vorliegen
        // (siehe SmtpEmailSender-Kommentar) - dann hier auf
        // AddTransient<IEmailSender, SmtpEmailSender>() umstellen.
        // Beim Umstellen auch den Text der Seite "Passwort vergessen"
        // (frontend/src/app/forgot-password/page.tsx: Erfolgstext, Untertitel
        // und Knopf) zurückändern: Sie sagen heute ehrlich, dass der Betreiber
        // das Passwort zurücksetzt, statt eine Mail zu versprechen, die
        // LoggingEmailSender nie verschickt. Auch die Antwort von
        // AuthController.ForgotPassword ("wurde ein Link verschickt") prüfen.
        services.AddTransient<IEmailSender, LoggingEmailSender>();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

        // Beim Start scheitern statt bei der ersten Anmeldung: Ein fehlender
        // Schlüssel (vergessene .env-Zeile) fiel sonst erst auf, wenn jemand
        // sich anmelden wollte. 32 Byte sind das Minimum für HMAC-SHA256.
        if (Encoding.UTF8.GetByteCount(jwtSettings.Secret) < 32)
            throw new InvalidOperationException(
                "Jwt:Secret fehlt oder ist kürzer als 32 Byte - mit `openssl rand -base64 48` erzeugen (siehe .env.example).");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
                };
                // Ein gültiger Token reicht nicht: Das Konto muss es noch geben,
                // und der Admin darf es nicht gesperrt haben (siehe KontoStatus).
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var status = context.HttpContext.RequestServices.GetRequiredService<KontoStatus>();
                        if (!Guid.TryParse(id, out var userId)
                            || !await status.IstNutzbarAsync(userId, context.HttpContext.RequestAborted))
                            context.Fail("Konto gesperrt oder gelöscht.");
                    },
                };
            });

        return services;
    }
}
