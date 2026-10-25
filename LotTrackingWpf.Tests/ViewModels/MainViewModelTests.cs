
using System.Collections.Generic;
using System.ComponentModel;
using LotTrackingWpf.Models;
using LotTrackingWpf.ViewModels;
using Xunit;

namespace LotTrackingWpf.Tests.ViewModels;

public class MainViewModelTests
{
    private static MainViewModel CreateViewModel()
    {
        // These tests exercise only ViewModel property logic.
        // They never execute database or pipeline commands.
        return new MainViewModel(null!, null!);
    }

    [Fact]
    public void SupplierName_WhenChanged_RaisesPropertyChanged()
    {
        // Arrange
        var vm = CreateViewModel();
        var changedProperties = new List<string?>();

        vm.PropertyChanged += (_, args) =>
            changedProperties.Add(args.PropertyName);

        // Act
        vm.SupplierName = "ABC Semiconductors";

        // Assert
        Assert.Equal("ABC Semiconductors", vm.SupplierName);
        Assert.Contains(nameof(vm.SupplierName), changedProperties);
    }

    [Fact]
    public void SelectedSupplier_WhenChanged_PopulatesSupplierFields()
    {
        // Arrange
        var vm = CreateViewModel();

        var supplier = new Supplier
        {
            Id = 1,
            SupplierName = "Intel",
            ContactEmail = "contact@example.com",
            Location = "Toronto"
        };

        // Act
        vm.SelectedSupplier = supplier;

        // Assert
        Assert.Equal("Intel", vm.SupplierName);
        Assert.Equal("contact@example.com", vm.SupplierEmail);
        Assert.Equal("Toronto", vm.SupplierLocation);
    }

    [Fact]
    public void SelectedLot_WhenChanged_UpdatesLotCode()
    {
        // Arrange
        var vm = CreateViewModel();

        var lot = new Lot
        {
            Id = 1,
            LotCode = "LOT001",
            Status = "CREATED"
        };

        // Act
        vm.SelectedLot = lot;

        // Assert
        Assert.Equal("LOT001", vm.LotCode);
    }

    [Fact]
    public void BulkCount_DefaultValue_Is100()
    {
        // Arrange
        var vm = CreateViewModel();

        // Assert
        Assert.Equal(100, vm.BulkCount);
    }
}
