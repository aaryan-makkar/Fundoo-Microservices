using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.Auth;
using Fundoo.User.Infrastructure.Notifications;
using Fundoo.User.Infrastructure.Persistence;
using Fundoo.User.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Npgsql;

namespace Fundoo.User.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUserInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection("Jwt").Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Jwt section is missing.");
        if (jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        var connectionString = config.GetConnectionString("UserDb")
    ?? throw new InvalidOperationException("ConnectionStrings:UserDb is missing.");

        var connectionStringBuilder =
            new NpgsqlConnectionStringBuilder(connectionString)
            {
                Pooling = true,
                MinPoolSize = 5,
                MaxPoolSize = 100,
                Timeout = 15,
                CommandTimeout = 30
            };

        connectionString = connectionStringBuilder.ConnectionString;
        services.AddDbContextPool<UserDbContext>(o =>
    o.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsAssembly(typeof(UserDbContext).Assembly.FullName)),
    poolSize: 128);

        services.AddSingleton(jwt);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        var notifyBaseUrl = config["Notification:BaseUrl"]
        ?? throw new InvalidOperationException("Notification:BaseUrl is missing.");
        var notifyKey = config["Notification:ApiKey"]
            ?? throw new InvalidOperationException("Notification:ApiKey is missing.");

        services.AddHttpClient<IEmailNotifier, NotificationApiEmailNotifier>(client =>
        {
            client.BaseAddress = new Uri(notifyBaseUrl);

            client.DefaultRequestHeaders.Add("X-Internal-Key", notifyKey);
        })
.AddStandardResilienceHandler(options =>
{
    // RETRY
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.Delay = TimeSpan.FromSeconds(1);

    // TIMEOUT
    options.TotalRequestTimeout.Timeout =
        TimeSpan.FromSeconds(15);

    options.AttemptTimeout.Timeout =
        TimeSpan.FromSeconds(5);

    // CIRCUIT BREAKER
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 5;
    options.CircuitBreaker.SamplingDuration =
        TimeSpan.FromSeconds(30);

    options.CircuitBreaker.BreakDuration =
        TimeSpan.FromSeconds(15);

    // CONCURRENCY LIMITER
    options.RateLimiter.DefaultRateLimiterOptions.PermitLimit = 20;

    options.RateLimiter.DefaultRateLimiterOptions.QueueLimit = 0;
});
        services.AddScoped<IResetTokenService, JwtResetTokenService>();
        services.AddScoped<IForgotPasswordService, ForgotPasswordService>();

        return services;
    }
}