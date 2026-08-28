using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace Gym.Services;

public sealed class DatabaseSchemaService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseSchemaService> _logger;

    public DatabaseSchemaService(
        IConfiguration configuration,
        ILogger<DatabaseSchemaService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", "GymSchema.sql");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("The Gym database schema script was not found.", scriptPath);

        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        var batches = Regex.Split(
            script,
            @"^\s*GO\s*(?:--.*)?$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master",
            // This project targets the local trusted SQL Express instance. Avoid
            // negotiating server encryption before the schema can be upgraded.
            Encrypt = false,
            TrustServerCertificate = true
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var batch in batches.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Gym database schema is ready.");
    }
}
