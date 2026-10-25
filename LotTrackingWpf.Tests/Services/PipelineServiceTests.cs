
using System;
using System.Threading.Tasks;
using LotTrackingWpf.Services;
using Xunit;

namespace LotTrackingWpf.Tests.Services;

public class PipelineServiceTests
{
    private static PipelineService CreateService()
    {
        // Only suitable for tests that do not access MySQL.
        return new PipelineService(null!, null!);
    }

    [Fact]
    public void GetPlantProgress_NoLot_ReturnsZero()
    {
        var service = CreateService();

        double progress = service.GetPlantProgress(
            plantNumber: 1,
            currentLotId: null);

        Assert.Equal(0, progress);
    }

    [Fact]
    public void GetPlantProgress_UnknownLot_ReturnsZero()
    {
        var service = CreateService();

        double progress = service.GetPlantProgress(
            plantNumber: 1,
            currentLotId: 100);

        Assert.Equal(0, progress);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void GetPlantProgress_NoActiveOperation_ReturnsZero(
        int plantNumber)
    {
        var service = CreateService();

        double progress = service.GetPlantProgress(
            plantNumber,
            currentLotId: 1);

        Assert.Equal(0, progress);
    }

    [Fact]
    public async Task Submit_BeforeInitialization_ThrowsException()
    {
        var service = CreateService();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.Submit(1));

        Assert.Equal(
            "Pipeline has not been initialized.",
            exception.Message);
    }

    [Fact]
    public async Task Submit_BeforeInitialization_DoesNotRequireDatabase()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.Submit(999));
    }
}
