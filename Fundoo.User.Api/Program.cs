using Fundoo.User.Infrastructure;
using Fundoo.User.Application.Abstractions;
using Asp.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUserInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection("Jwt");

var signingKey = jwt["SigningKey"];

if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be at least 32 characters.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                var body = JsonSerializer.SerializeToUtf8Bytes(
                    new ProblemDetails
                    {
                        Title = "Sign-in required",
                        Status = StatusCodes.Status401Unauthorized,
                        Detail = "Your sign-in session is invalid or has expired. Please sign in again."
                    },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                await context.Response.Body.WriteAsync(
                    body,
                    context.HttpContext.RequestAborted);
            },
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (!Guid.TryParse(principal?.FindFirst("sid")?.Value, out var sessionId) ||
                    !Guid.TryParse(principal?.FindFirst("sub")?.Value, out var userId))
                {
                    context.Fail("The token is not associated with a valid session.");
                    return;
                }

                var sessions = context.HttpContext.RequestServices
                    .GetRequiredService<IUserRepository>();
                var session = await sessions.FindSessionByIdAsync(
                    sessionId,
                    context.HttpContext.RequestAborted);

                if (session is null || session.UserId != userId ||
                    session.RevokedAtUtc is not null ||
                    session.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    context.Fail("The session is invalid or has ended.");
                }
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],

            ValidateAudience = true,
            ValidAudience = jwt["Audience"],

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(signingKey)),

            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = false;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Fundoo User API",
        Version = "v1"
    });

    options.SwaggerDoc("v2", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Fundoo User API",
        Version = "v2"
    });

    options.DocInclusionPredicate((documentName, apiDescription) =>
        apiDescription.GroupName == documentName);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fundoo User API V1");
        options.SwaggerEndpoint("/swagger/v2/swagger.json", "Fundoo User API V2");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", async (
    Fundoo.User.Infrastructure.Persistence.UserDbContext db,
    CancellationToken cancellationToken) =>
{
    var connected = await db.Database.CanConnectAsync(cancellationToken);

    return connected
        ? Results.Ok(new
        {
            status = "Healthy",
            database = "Connected"
        })
        : Results.Problem(
            title: "User API is unhealthy",
            statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.Run();
