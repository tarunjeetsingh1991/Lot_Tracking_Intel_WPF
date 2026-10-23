
using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace LotTrackingWpf.Data;

public sealed class Database
{
    public string ConnectionString { get; }

    public Database()
    {
        string filePath = Path.Combine(
            AppContext.BaseDirectory,
            "appsettings.json"
        );

        using var json = JsonDocument.Parse(
            File.ReadAllText(filePath)
        );

        ConnectionString = json.RootElement
            .GetProperty("ConnectionString")
            .GetString()
            ?? throw new Exception("Missing ConnectionString");
    }

    public LotTrackingDbContext Open()
    {
        var options = new DbContextOptionsBuilder<LotTrackingDbContext>()
            .UseMySql(
                ConnectionString,
                ServerVersion.AutoDetect(ConnectionString)
            )
            .Options;

        return new LotTrackingDbContext(options);
    }

    public async Task InitializeAsync()
    {
        var builder = new MySqlConnectionStringBuilder(ConnectionString);

        var db = builder.Database;

        if (string.IsNullOrWhiteSpace(db) ||
            !Regex.IsMatch(db, "^[A-Za-z0-9_]+$"))
        {
            throw new Exception("Invalid database name");
        }

        builder.Database = "";

        await using (var server = new MySqlConnection(builder.ConnectionString))
        {
            await server.OpenAsync();

            await using var command = server.CreateCommand();

            command.CommandText =
                $"CREATE DATABASE IF NOT EXISTS `{db}` CHARACTER SET utf8mb4";

            await command.ExecuteNonQueryAsync();
        }

        await using var ctx = Open();

        await ctx.Database.EnsureCreatedAsync();

        for (int i = 1; i <= 10; i++)
        {
            bool plantExists = await ctx.Plants
                .AnyAsync(p => p.PlantNumber == i);

            if (!plantExists)
            {
                ctx.Plants.Add(new Models.Plant
                {
                    PlantNumber = i,
                    PlantName = $"Plant {i}",
                    BaseProcessingTimeMs = 500L * i
                });
            }
        }

        await ctx.SaveChangesAsync();
    }
}
