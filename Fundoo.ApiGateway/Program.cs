using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

namespace Fundoo.ApiGateway
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddJsonFile(
                "ocelot.json",
                optional: false,
                reloadOnChange: true);

            var signingKey = builder.Configuration["Jwt:SigningKey"]
                ?? throw new InvalidOperationException(
                    "Jwt:SigningKey is missing.");

            builder.Services
                .AddAuthentication()
                .AddJwtBearer("Bearer", options =>
                {
                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey =
                                new SymmetricSecurityKey(
                                    Encoding.UTF8.GetBytes(signingKey)),
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            ClockSkew = TimeSpan.Zero
                        };
                });

            // CONCURRENCY LIMITER
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode =
                    StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter =
                    PartitionedRateLimiter.Create<HttpContext, string>(
                        context =>
                            RateLimitPartition.GetConcurrencyLimiter(
                                "gateway-global",
                                _ => new ConcurrencyLimiterOptions
                                {
                                    PermitLimit = 50,
                                    QueueLimit = 0,
                                    QueueProcessingOrder =
                                        QueueProcessingOrder.OldestFirst
                                }));
            });

            builder.Services.AddHttpClient();

            builder.Services.AddOcelot();

            var app = builder.Build();

            app.UseRateLimiter();

            app.Map("/health", branch =>
            {
                branch.Run(async context =>
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(
                        "{\"status\":\"Healthy\"}");
                });
            });

            await app.UseOcelot();

            app.Run();
        }
    }
}