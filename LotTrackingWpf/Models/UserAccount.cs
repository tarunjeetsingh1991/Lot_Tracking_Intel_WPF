
namespace LotTrackingWpf.Models;

public sealed class UserAccount
{
    public long Id { get; set; }

    public string Username { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public string Role { get; set; } = "ADMIN";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
