using Dogity.Application.Abstractions;
using Dogity.Infrastructure.Email;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Import;
using Dogity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
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
            .AddDefaultTokenProviders();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IRegulationPdfParser, RegulationPdfParser>();

        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        // LoggingEmailSender ist aktiv, bis echte SMTP-Zugangsdaten vorliegen
        // (siehe SmtpEmailSender-Kommentar) - dann hier auf
        // AddTransient<IEmailSender, SmtpEmailSender>() umstellen.
        services.AddTransient<IEmailSender, LoggingEmailSender>();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

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
            });

        return services;
    }
}
