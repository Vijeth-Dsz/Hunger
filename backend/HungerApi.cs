using Hunger.Api;
using Hunger.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
var adminName = builder.Configuration["Admin:Name"]
    ?? throw new InvalidOperationException("Admin:Name is not configured.");
var adminEmail = builder.Configuration["Admin:Email"]
    ?? throw new InvalidOperationException("Admin:Email is not configured.");
var adminOrganization = builder.Configuration["Admin:OrganizationName"]
    ?? throw new InvalidOperationException("Admin:OrganizationName is not configured.");
var adminPassword = builder.Configuration["Admin:Password"]
    ?? throw new InvalidOperationException("Admin:Password is not configured.");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "Hunger.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    app.Logger.LogError(context.Features.Get<IExceptionHandlerFeature>()?.Error, "API request failed.");
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { error = "An unexpected server error occurred." });
}));
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/health", async (CancellationToken ct) =>
{
    await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
    await connection.OpenAsync(ct);
    return Results.Ok(new { status = "ok", application = "Hunger" });
});
AuthEndpoints.Map(app, connectionString);
DonationEndpoints.Map(app, connectionString);
await DatabaseInitializer.InitializeAsync(connectionString, app.Logger);
await AuthEndpoints.SeedAdminAsync(connectionString, adminName, adminEmail, adminOrganization, adminPassword);
app.Logger.LogInformation("Hunger API ready.");
app.Run();
