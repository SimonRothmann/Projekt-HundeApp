using Dogity.Application.Abstractions;
using Dogity.Application.Notifications;
using Dogity.Infrastructure.Email;
using Dogity.Infrastructure.Geocoding;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Weather;
using Dogity.Infrastructure.Import;
using Dogity.Infrastructure.Notifications;
using Dogity.Infrastructure.Push;
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

        // E-Mail: Der Host entscheidet. Ist "Email:Smtp:Host" gesetzt, wird per
        // SMTP versendet, sonst nur protokolliert (LoggingEmailSender) - ohne
        // Codewechsel zum Aktivieren. Versendet wird nie im Request, sondern vom
        // Hintergrunddienst hinter der Warteschlange (MailVersandBackgroundService).
        // Zum Aktivieren siehe docs/BETRIEB.md "E-Mail und Push aktivieren".
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        if (!string.IsNullOrWhiteSpace(configuration[$"{SmtpSettings.SectionName}:Host"]))
            services.AddTransient<IEmailSender, SmtpEmailSender>();
        else
            services.AddTransient<IEmailSender, LoggingEmailSender>();

        services.AddSingleton<MailQueue>();
        services.AddSingleton<IMailQueue>(sp => sp.GetRequiredService<MailQueue>());
        services.AddSingleton<MailVersender>();

        // Push: nur mit vollständigem VAPID-Schlüsselpaar aktiv (siehe
        // PushSettings); ohne tut WebPushSender nichts. Typisierter HttpClient
        // mit kurzem Timeout: Ein träger Push-Dienst soll den Versand der übrigen
        // Geräte nicht ewig aufhalten.
        services.Configure<PushSettings>(configuration.GetSection(PushSettings.SectionName));
        // Ohne Weiterleitungen: Angefragt werden nur die freigegebenen Push-Dienste
        // (PushEndpointPruefung); eine Umleitung führte an dieser Prüfung vorbei.
        services.AddHttpClient<IPushSender, WebPushSender>(client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<PushQueue>();
        services.AddSingleton<IPushQueue>(sp => sp.GetRequiredService<PushQueue>());

        services.AddSingleton<INotificationChannels, NotificationChannels>();
        services.AddSingleton<IFrontendLinks, FrontendLinks>();
        services.AddSingleton<IEmailAbmeldeToken, EmailAbmeldeToken>();

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
