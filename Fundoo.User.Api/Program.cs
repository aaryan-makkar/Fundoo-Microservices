using Fundoo.User.Infrastructure;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUserInfrastructure(builder.Configuration);

builder.Services.AddControllers();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);

    options.AssumeDefaultVersionWhenUnspecified = false;

    options.ReportApiVersions = true;

    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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