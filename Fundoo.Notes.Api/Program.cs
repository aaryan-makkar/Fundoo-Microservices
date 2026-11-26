using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Fundoo.Notes.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

var userApiBaseUrl = builder.Configuration["UserApi:BaseUrl"];
if (!Uri.TryCreate(userApiBaseUrl, UriKind.Absolute, out var userApiUri))
    throw new InvalidOperationException("UserApi:BaseUrl must be an absolute URL.");

builder.Services.AddNotesInfrastructure(builder.Configuration);
builder.Services.AddHttpClient("UserApi", client =>
{
    client.BaseAddress = userApiUri;
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;   // keep the claim name "sub"
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var authorization = context.Request.Headers.Authorization.ToString();
                if (!AuthenticationHeaderValue.TryParse(authorization, out var bearer) ||
                    !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(bearer.Parameter))
                {
                    context.Fail("A valid bearer token is required.");
                    return;
                }

                try
                {
                    var clientFactory = context.HttpContext.RequestServices
                        .GetRequiredService<IHttpClientFactory>();
                    using var request = new HttpRequestMessage(
                        HttpMethod.Get,
                        "api/v1.0/auth/me");
                    request.Headers.Authorization = bearer;

                    using var response = await clientFactory.CreateClient("UserApi")
                        .SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            context.HttpContext.RequestAborted);

                    if ((int)response.StatusCode >= 500)
                    {
                        context.HttpContext.Items["SessionValidationUnavailable"] = true;
                        context.Fail("Session validation is unavailable.");
                    }
                    else if (!response.IsSuccessStatusCode)
                    {
                        context.Fail("The sign-in session is invalid or has ended.");
                    }
                }
                catch (HttpRequestException)
                {
                    context.HttpContext.Items["SessionValidationUnavailable"] = true;
                    context.Fail("Session validation is unavailable.");
                }
                catch (TaskCanceledException) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
                {
                    context.HttpContext.Items["SessionValidationUnavailable"] = true;
                    context.Fail("Session validation is unavailable.");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var unavailable = context.HttpContext.Items.ContainsKey("SessionValidationUnavailable");
                var status = unavailable
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status401Unauthorized;
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/problem+json";

                var body = JsonSerializer.SerializeToUtf8Bytes(
                    new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Title = unavailable ? "Session validation unavailable" : "Sign-in required",
                        Status = status,
                        Detail = unavailable
                            ? "Fundoo could not validate your session right now. Please retry."
                            : "Your sign-in session is invalid or has expired. Please sign in again."
                    },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                await context.Response.Body.WriteAsync(
                    body,
                    context.HttpContext.RequestAborted);
            }
        };
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Adds the "Authorize" button in Swagger so you can paste the token
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (
    Npgsql.NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    try
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);

        return Results.Ok(new
        {
            status = "Healthy",
            database = "Connected"
        });
    }
    catch
    {
        return Results.Problem(
            title: "Notes API is unhealthy",
            detail: "Database connection failed.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.Run();
