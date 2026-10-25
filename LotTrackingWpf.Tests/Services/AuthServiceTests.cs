
using System;
using System.Threading.Tasks;
using LotTrackingWpf.Services;
using Xunit;

namespace LotTrackingWpf.Tests.Services;

public class AuthServiceTests
{
    private readonly AuthService service;

    public AuthServiceTests()
    {
        // These tests exercise validation paths that
        // return or throw before Database.Open().
        service = new AuthService(null!);
    }

    // TEST 1
    [Fact]
    public async Task LoginAsync_NullUsername_ReturnsNull()
    {
        var result = await service.LoginAsync(
            null!,
            "Password123!");

        Assert.Null(result);
    }

    // TEST 2
    [Fact]
    public async Task LoginAsync_EmptyUsername_ReturnsNull()
    {
        var result = await service.LoginAsync(
            "",
            "Password123!");

        Assert.Null(result);
    }

    // TEST 3
    [Fact]
    public async Task LoginAsync_WhitespaceUsername_ReturnsNull()
    {
        var result = await service.LoginAsync(
            "   ",
            "Password123!");

        Assert.Null(result);
    }

    // TEST 4
    [Fact]
    public async Task LoginAsync_NullPassword_ReturnsNull()
    {
        var result = await service.LoginAsync(
            "admin",
            null!);

        Assert.Null(result);
    }

    // TEST 5
    [Fact]
    public async Task LoginAsync_EmptyPassword_ReturnsNull()
    {
        var result = await service.LoginAsync(
            "admin",
            "");

        Assert.Null(result);
    }

    // TEST 6
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterInitialAdminAsync_InvalidUsername_ThrowsException(
        string? username)
    {
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.RegisterInitialAdminAsync(
                    username!,
                    "Password123!"));

        Assert.Contains(
            "Username",
            exception.Message);
    }

    // TEST 7
    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    public async Task RegisterInitialAdminAsync_ShortUsername_ThrowsException(
        string username)
    {
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.RegisterInitialAdminAsync(
                    username,
                    "Password123!"));

        Assert.Equal(
            "Username must contain 3–100 characters.",
            exception.Message);
    }

    // TEST 8
    [Fact]
    public async Task RegisterInitialAdminAsync_UsernameOver100Characters_ThrowsException()
    {
        string username = new string('a', 101);

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.RegisterInitialAdminAsync(
                    username,
                    "Password123!"));

        Assert.Equal(
            "Username must contain 3–100 characters.",
            exception.Message);
    }

    // TEST 9
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("123456789")]
    public async Task RegisterInitialAdminAsync_ShortPassword_ThrowsException(
        string? password)
    {
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.RegisterInitialAdminAsync(
                    "admin",
                    password!));

        Assert.Equal(
            "Password must contain at least 10 characters.",
            exception.Message);
    }

    // TEST 10
    [Fact]
    public async Task RegisterInitialAdminAsync_WhitespacePassword_ThrowsException()
    {
        // A password containing 9 spaces is still
        // rejected because its length is below 10.
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.RegisterInitialAdminAsync(
                    "admin",
                    "         "));

        Assert.Equal(
            "Password must contain at least 10 characters.",
            exception.Message);
    }

    // TEST 11
    [Fact]
    public async Task RegisterInitialAdminAsync_TrimmedUsernameTooShort_ThrowsException()
    {
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.RegisterInitialAdminAsync(
                    "  ab  ",
                    "Password123!"));

        Assert.Equal(
            "Username must contain 3–100 characters.",
            exception.Message);
    }

    // TEST 12
    [Fact]
    public async Task RegisterInitialAdminAsync_UsernameExactly101Characters_ThrowsException()
    {
        string username = new string('X', 101);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RegisterInitialAdminAsync(
                username,
                "Password123!"));
    }
}
