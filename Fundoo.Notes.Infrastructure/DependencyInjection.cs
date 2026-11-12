using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Labels;
using Fundoo.Notes.Application.Notes;
using Fundoo.Notes.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fundoo.Notes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("NotesDb")
            ?? throw new InvalidOperationException("ConnectionStrings:NotesDb is missing.");

        // One data source for the whole app (Npgsql handles connection pooling itself)
        var connectionStringBuilder =
     new NpgsqlConnectionStringBuilder(connectionString)
     {
         Pooling = true,
         MinPoolSize = 5,
         MaxPoolSize = 100,
         Timeout = 15,
         CommandTimeout = 30
     };

        services.AddSingleton(_ =>
            NpgsqlDataSource.Create(connectionStringBuilder.ConnectionString));

        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<ILabelService, LabelService>();
        return services;
    }
}