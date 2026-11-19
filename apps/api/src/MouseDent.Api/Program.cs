using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MouseDent.Api.Auth;
using MouseDent.Api.Common;
using MouseDent.Api.Locales;
using MouseDent.Application;
using MouseDent.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddApiDocumentation();
builder.Services.AddApiRateLimiting(builder.Configuration);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length > 0)
    {
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    }
}));

var app = builder.Build();

app.EnsureDeployableSigningKey();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponse.WriteJsonAsync,
});

app.MapApiDocumentation();
app.MapAuthEndpoints();
app.MapStaffAccountEndpoints();
app.MapLocalEndpoints();

app.Run();

/// <summary>Punto de entrada expuesto para las pruebas de integración.</summary>
public partial class Program;
