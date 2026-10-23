
using System;
using System.Data;
using System.Threading.Tasks;
using LotTrackingWpf.Data;
using LotTrackingWpf.Models;
using Microsoft.EntityFrameworkCore;

namespace LotTrackingWpf.Services;

public sealed class AuthService
{
    private readonly Database database;

    public AuthService(Database database)
    {
        this.database = database;
    }

    public async Task<bool> HasUsersAsync()
    {
        await using var db = database.Open();

        return await db.Users.AnyAsync();
    }

    public async Task<UserAccount?> LoginAsync(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(password))
        {
            return null;
        }

        username = username.Trim();

        await using var db = database.Open();

        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Username == username);

        if (user == null)
        {
            return null;
        }

        bool passwordValid = BCrypt.Net.BCrypt.Verify(
            password,
            user.PasswordHash);

        return passwordValid ? user : null;
    }

    public async Task RegisterInitialAdminAsync(
        string username,
        string password)
    {
        username = username?.Trim()
            ?? throw new ArgumentException(
                "Username is required.");

        if (username.Length < 3 || username.Length > 100)
        {
            throw new ArgumentException(
                "Username must contain 3–100 characters.");
        }

        if (string.IsNullOrEmpty(password) ||
            password.Length < 10)
        {
            throw new ArgumentException(
                "Password must contain at least 10 characters.");
        }

        await using var db = database.Open();

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        if (await db.Users.AnyAsync())
        {
            throw new InvalidOperationException(
                "Administrator already exists.");
        }

        // Password is hashed before being sent to MySQL.
        string hashedPassword =
            BCrypt.Net.BCrypt.HashPassword(
                password,
                workFactor: 12);

        var admin = new UserAccount
        {
            Username = username,
            PasswordHash = hashedPassword,
            Role = "ADMIN",
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(admin);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
