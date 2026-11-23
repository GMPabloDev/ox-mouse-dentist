using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MouseDent.Application.Auth;
using MouseDent.Application.Catalogo;
using MouseDent.Application.Locales;
using MouseDent.Application.Profesionales;
using MouseDent.Domain.Profesionales;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Catalogo;
using MouseDent.Infrastructure.Locales;
using MouseDent.Infrastructure.Persistence;
using MouseDent.Infrastructure.Profesionales;

namespace MouseDent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);

        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=mousedent.db";
        services.AddDbContext<MouseDentDbContext>(options =>
            options.UseSqlite(ResolveConnectionString(connectionString, environment.ContentRootPath)));

        services.AddIdentityCore<StaffUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<MouseDentDbContext>()
            .AddSignInManager();

        services.AddScoped<IStaffAccounts, EfStaffAccounts>();
        services.AddScoped<IStaffAccessLinks, EfStaffAccessLinks>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        // La URL pública del correo y el SMTP se configuran fuera del repositorio.
        // No se valida al arrancar: la generación de OpenAPI no debe exigir secretos.
        // La lectura es diferida para que el host de pruebas y los despliegues
        // apliquen su configuración final.
        services.AddSingleton(provider =>
            provider.GetRequiredService<IConfiguration>()
                .GetSection(StaffAccessOptions.SectionName)
                .Get<StaffAccessOptions>()
            ?? new StaffAccessOptions());

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName));

        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddOptions<LocalPhotoOptions>()
            .Bind(configuration.GetSection(LocalPhotoOptions.SectionName));

        services.AddSingleton<FileSystemLocalPhotos>();
        services.AddSingleton<ILocalPhotos>(provider => provider.GetRequiredService<FileSystemLocalPhotos>());
        services.AddHostedService<LocalPhotoStorageInitializationService>();

        services.AddScoped<ILocales, EfLocales>();
        services.AddScoped<ICatalogo, EfCatalogo>();
        services.AddScoped<IProfesionales, EfProfesionales>();
        services.AddScoped<IAltasDeProfesionales, EfAltasDeProfesionales>();
        services.AddScoped<IListadoDeProfesionales, EfListadoDeProfesionales>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // La clave se construye al resolver las opciones, no al registrar servicios:
        // así la generación de OpenAPI puede arrancar el host sin un secreto de producción.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                bearer.MapInboundClaims = false;
                bearer.Events = new JwtBearerEvents
                {
                    // Cada solicitud autenticada compara el token con el estado persistido.
                    OnTokenValidated = StaffSessionRevalidation.ValidateAsync,
                };
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AuthClaims.Name,
                    RoleClaimType = AuthClaims.Role,
                };
            });

        services.AddAuthorization();

        services.AddHealthChecks()
            .AddDbContextCheck<MouseDentDbContext>("database");

        if (configuration.GetValue("Database:AutoMigrate", environment.IsDevelopment()))
        {
            services.AddHostedService<DatabaseInitializationService>();
        }

        return services;
    }

    private static string ResolveConnectionString(string connectionString, string contentRootPath)
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(builder.DataSource) && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
        }

        return builder.ToString();
    }
}
