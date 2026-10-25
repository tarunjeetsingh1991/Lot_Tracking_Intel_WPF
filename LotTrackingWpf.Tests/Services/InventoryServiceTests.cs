
using System;
using System.Threading.Tasks;
using LotTrackingWpf.Services;
using Xunit;

namespace LotTrackingWpf.Tests.Services;

public class InventoryServiceTests
{
    private static InventoryService CreateService()
    {
        // Safe only for tests where validation occurs
        // before any database operation.
        return new InventoryService(null!);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task AddSupplier_EmptyName_ThrowsException(
        string name)
    {
        var service = CreateService();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddSupplier(
                    name,
                    "test@example.com",
                    "Toronto"));

        Assert.Equal(
            "Supplier name is required.",
            exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddWafer_EmptyCode_ThrowsException(
        string code)
    {
        var service = CreateService();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddWafer(
                    code,
                    "Silicon",
                    1));

        Assert.Equal(
            "Wafer code is required.",
            exception.Message);
    }

    [Fact]
    public async Task AddWafer_EmptyType_ThrowsException()
    {
        var service = CreateService();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddWafer(
                    "WAFER1",
                    " ",
                    1));

        Assert.Equal(
            "Wafer type is required.",
            exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    [InlineData(1000)]
    public async Task BulkWafers_InvalidCount_ThrowsException(
        int count)
    {
        var service = CreateService();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.BulkWafers(
                    "Silicon",
                    1,
                    count));

        Assert.Equal(
            "Bulk count must be between 1 and 500.",
            exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AddLot_EmptyCode_ThrowsException(
        string code)
    {
        var service = CreateService();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddLot(code));

        Assert.Equal(
            "Lot code is required.",
            exception.Message);
    }

    [Fact]
    public async Task Assign_NullWaferIds_ThrowsException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.Assign(1, null!));
    }

    [Fact]
    public async Task Assign_EmptyWaferIds_ThrowsException()
    {
        var service = CreateService();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.Assign(1, Array.Empty<long>()));

        Assert.Equal(
            "Select at least one wafer.",
            exception.Message);
    }

    [Fact]
    public async Task Assign_MoreThan24Wafers_ThrowsException()
    {
        var service = CreateService();

        var waferIds = new long[25];

        for (int i = 0; i < waferIds.Length; i++)
        {
            waferIds[i] = i + 1;
        }

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.Assign(1, waferIds));

        Assert.Equal(
            "Maximum 24 wafers per lot.",
            exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(600001)]
    public async Task SetPlantTime_InvalidDuration_ThrowsException(
        long milliseconds)
    {
        var service = CreateService();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SetPlantTime(1, milliseconds));

        Assert.Equal(
            "Plant time must be between 1 and 600000 ms.",
            exception.Message);
    }
}
