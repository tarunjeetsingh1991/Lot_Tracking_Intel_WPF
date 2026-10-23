
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using LotTrackingWpf.Infrastructure;
using LotTrackingWpf.Models;
using LotTrackingWpf.Services;

namespace LotTrackingWpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly InventoryService inv;
    private readonly PipelineService pipeline;

    private bool restoringSelections;

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<Wafer> Wafers { get; } = new();
    public ObservableCollection<Lot> Lots { get; } = new();

    // Only lots that can still receive wafers.
    public ObservableCollection<Lot> AssignableLots { get; } = new();

    public ObservableCollection<Plant> Plants { get; } = new();
    public ObservableCollection<ProcessLog> Logs { get; } = new();

    [ObservableProperty]
    private Supplier? selectedSupplier;

    [ObservableProperty]
    private Wafer? selectedWafer;

    [ObservableProperty]
    private Lot? selectedLot;

    [ObservableProperty]
    private Plant? selectedPlant;

    [ObservableProperty]
    private string supplierName = "";

    [ObservableProperty]
    private string supplierEmail = "";

    [ObservableProperty]
    private string supplierLocation = "";

    [ObservableProperty]
    private string waferCode = "";

    [ObservableProperty]
    private string waferType = "Silicon";

    [ObservableProperty]
    private Supplier? waferSupplier;

    [ObservableProperty]
    private int bulkCount = 100;

    [ObservableProperty]
    private string lotCode = "";

    [ObservableProperty]
    private Lot? assignmentLot;

    [ObservableProperty]
    private string waferIds = "";

    [ObservableProperty]
    private long plantTimeMs = 1000;

    [ObservableProperty]
    private string summary = "Loading...";

    public ICommand RefreshCommand { get; }
    public ICommand AddSupplierCommand { get; }
    public ICommand UpdateSupplierCommand { get; }
    public ICommand DeleteSupplierCommand { get; }
    public ICommand AddWaferCommand { get; }
    public ICommand BulkWaferCommand { get; }
    public ICommand UpdateWaferCommand { get; }
    public ICommand DeleteWaferCommand { get; }
    public ICommand AddLotCommand { get; }
    public ICommand UpdateLotCommand { get; }
    public ICommand DeleteLotCommand { get; }
    public ICommand AssignCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand SetPlantTimeCommand { get; }

    public MainViewModel(
        InventoryService inv,
        PipelineService pipeline)
    {
        this.inv = inv;
        this.pipeline = pipeline;

        RefreshCommand = Cmd(Refresh);

        AddSupplierCommand = Cmd(async () =>
        {
            await inv.AddSupplier(
                SupplierName,
                SupplierEmail,
                SupplierLocation);

            await Refresh();
        });

        UpdateSupplierCommand = Cmd(async () =>
        {
            if (SelectedSupplier is null)
                return;

            await inv.UpdateSupplier(
                SelectedSupplier.Id,
                SupplierName,
                SupplierEmail,
                SupplierLocation);

            await Refresh();
        });

        DeleteSupplierCommand = Cmd(async () =>
        {
            if (SelectedSupplier is null)
                return;

            await inv.DeleteSupplier(SelectedSupplier.Id);
            await Refresh();
        });

        AddWaferCommand = Cmd(async () =>
        {
            await inv.AddWafer(
                WaferCode,
                WaferType,
                WaferSupplier?.Id
                    ?? throw new Exception("Select supplier"));

            await Refresh();
        });

        BulkWaferCommand = Cmd(async () =>
        {
            await inv.BulkWafers(
                WaferType,
                WaferSupplier?.Id
                    ?? throw new Exception("Select supplier"),
                BulkCount);

            await Refresh();
        });

        UpdateWaferCommand = Cmd(async () =>
        {
            if (SelectedWafer is null)
                return;

            await inv.UpdateWafer(
                SelectedWafer.Id,
                WaferCode,
                WaferType,
                WaferSupplier?.Id
                    ?? throw new Exception("Select supplier"));

            await Refresh();
        });

        DeleteWaferCommand = Cmd(async () =>
        {
            if (SelectedWafer is null)
                return;

            await inv.DeleteWafer(SelectedWafer.Id);
            await Refresh();
        });

        AddLotCommand = Cmd(async () =>
        {
            await inv.AddLot(LotCode);
            await Refresh();
        });

        UpdateLotCommand = Cmd(async () =>
        {
            if (SelectedLot is null)
                return;

            await inv.UpdateLot(SelectedLot.Id, LotCode);
            await Refresh();
        });

        DeleteLotCommand = Cmd(async () =>
        {
            if (SelectedLot is null)
                return;

            await inv.DeleteLot(SelectedLot.Id);
            await Refresh();
        });

        AssignCommand = Cmd(async () =>
        {
            if (AssignmentLot is null)
                throw new InvalidOperationException("Select a lot.");

            // Prevent assignment to lots that have started processing.
            if (!IsAssignable(AssignmentLot))
            {
                throw new InvalidOperationException(
                    "This lot has already started processing " +
                    "or is no longer available for assignment.");
            }

            long[] ids = WaferIds.Split(
                    new[] { ',', ';', ' ', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(long.Parse)
                .Distinct()
                .ToArray();

            await inv.Assign(AssignmentLot.Id, ids);

            WaferIds = string.Empty;

            await Refresh();
        });

        StartCommand = Cmd(async () =>
        {
            if (SelectedLot is null)
                throw new Exception("Select lot");

            await pipeline.Submit(SelectedLot.Id);
            await Refresh();
        });

        SetPlantTimeCommand = Cmd(async () =>
        {
            if (SelectedPlant is null)
                throw new Exception("Select plant");

            await inv.SetPlantTime(
                SelectedPlant.PlantNumber,
                PlantTimeMs);

            await Refresh();
        });
    }

    private static ICommand Cmd(Func<Task> action)
    {
        return new AsyncCommand(action);
    }

    // A lot becomes permanently ineligible for assignment
    // once processing has started.
    private static bool IsAssignable(Lot lot)
    {
        return !lot.ProcessingStarted &&
               (lot.Status == "CREATED" ||
                lot.Status == "READY_FOR_PROCESSING");
    }

    partial void OnSelectedSupplierChanged(Supplier? supplier)
    {
        if (restoringSelections || supplier is null)
            return;

        SupplierName = supplier.SupplierName;
        SupplierEmail = supplier.ContactEmail;
        SupplierLocation = supplier.Location;
    }

    partial void OnSelectedWaferChanged(Wafer? wafer)
    {
        if (restoringSelections || wafer is null)
            return;

        WaferCode = wafer.WaferCode;
        WaferType = wafer.WaferType;

        WaferSupplier = Suppliers.FirstOrDefault(
            s => s.Id == wafer.SupplierId);
    }

    partial void OnSelectedLotChanged(Lot? lot)
    {
        if (restoringSelections || lot is null)
            return;

        LotCode = lot.LotCode;
    }

    partial void OnSelectedPlantChanged(Plant? plant)
    {
        if (restoringSelections || plant is null)
            return;

        PlantTimeMs = plant.BaseProcessingTimeMs;
    }

    public async Task Refresh()
    {
        var suppliers = await inv.Suppliers();
        var wafers = await inv.Wafers();
        var lots = await inv.Lots();
        var plants = await inv.Plants();
        var logs = await inv.Logs();

        long? supplierId = SelectedSupplier?.Id;
        long? waferId = SelectedWafer?.Id;
        long? lotId = SelectedLot?.Id;
        long? assignmentLotId = AssignmentLot?.Id;
        int? plantNumber = SelectedPlant?.PlantNumber;
        long? waferSupplierId = WaferSupplier?.Id;

        restoringSelections = true;

        try
        {
            Synchronize(
                Suppliers,
                suppliers,
                x => x.Id,
                x => $"{x.SupplierName}|{x.ContactEmail}|{x.Location}");

            Synchronize(
                Wafers,
                wafers,
                x => x.Id,
                x => $"{x.WaferCode}|{x.WaferType}|{x.SupplierId}|" +
                     $"{x.LotId}|{x.Status}|{x.SupplierName}");

            Synchronize(
                Lots,
                lots,
                x => x.Id,
                x => $"{x.LotCode}|{x.Status}|{x.CurrentPlantNumber}|" +
                     $"{x.TotalWafers}|{x.ProcessingStarted}|{x.CompletedAt}");

            // Synchronize only eligible lots into the assignment dropdown.
            // Reuse the Lot instances from the main Lots collection,
            // so SelectedItem binding works correctly.
            var eligibleLots = Lots
                .Where(IsAssignable)
                .ToList();

            Synchronize(
                AssignableLots,
                eligibleLots,
                x => x.Id,
                x => $"{x.LotCode}|{x.Status}|" +
                     $"{x.TotalWafers}|{x.ProcessingStarted}");

            Synchronize(
                Plants,
                plants,
                x => x.PlantNumber,
                x => $"{x.PlantName}|{x.BaseProcessingTimeMs}|" +
                     $"{x.Occupied}|{x.CurrentLotId}|{x.CurrentLotCode}");

            Synchronize(
                Logs,
                logs,
                x => x.Id,
                x => x.Message);

            SelectedSupplier = Suppliers.FirstOrDefault(
                x => x.Id == supplierId);

            SelectedWafer = Wafers.FirstOrDefault(
                x => x.Id == waferId);

            SelectedLot = Lots.FirstOrDefault(
                x => x.Id == lotId);

            // Restore only from eligible lots.
            // Completed or processing lots are never selected here.
            AssignmentLot = AssignableLots.FirstOrDefault(
                x => x.Id == assignmentLotId);

            if (AssignmentLot is null)
            {
                WaferIds = string.Empty;
            }

            SelectedPlant = Plants.FirstOrDefault(
                x => x.PlantNumber == plantNumber);

            WaferSupplier = Suppliers.FirstOrDefault(
                x => x.Id == waferSupplierId);
        }
        finally
        {
            restoringSelections = false;
        }

        Summary =
            $"Suppliers: {suppliers.Count}    " +
            $"Wafers: {wafers.Count}    " +
            $"Lots: {lots.Count}    " +
            $"Active: {lots.Count(x => x.Status is "IN_PROGRESS" or "WAITING")}    " +
            $"Completed: {lots.Count(x => x.Status == "COMPLETED")}    " +
            $"Failed: {lots.Count(x => x.Status == "FAILED")}    " +
            $"Occupied plants: {plants.Count(x => x.Occupied)}";

        UpdatePlantProgress();
    }

    // Update progress without querying MySQL. Called by the fast UI timer.
    public void UpdatePlantProgress()
    {
        for (int i = 0; i < Plants.Count; i++)
        {
            Plant plant = Plants[i];
            double progress = plant.Occupied
                ? pipeline.GetPlantProgress(plant.PlantNumber, plant.CurrentLotId)
                : 0;

            if (plant.ProcessingProgress == progress)
                continue;

            // Plant does not implement INotifyPropertyChanged, so replace
            // the collection item to notify the DataGrid.
            var updated = new Plant
            {
                PlantNumber = plant.PlantNumber,
                PlantName = plant.PlantName,
                BaseProcessingTimeMs = plant.BaseProcessingTimeMs,
                Occupied = plant.Occupied,
                CurrentLotId = plant.CurrentLotId,
                CurrentLotCode = plant.CurrentLotCode,
                ProcessingProgress = progress
            };

            bool wasSelected = ReferenceEquals(SelectedPlant, plant);
            Plants[i] = updated;

            if (wasSelected)
            {
                restoringSelections = true;
                try { SelectedPlant = updated; }
                finally { restoringSelections = false; }
            }
        }
    }

    private static void Synchronize<T, TKey>(
        ObservableCollection<T> target,
        IReadOnlyList<T> source,
        Func<T, TKey> key,
        Func<T, string> signature)
        where TKey : notnull
    {
        var incomingKeys = source
            .Select(key)
            .ToHashSet();

        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!incomingKeys.Contains(key(target[i])))
            {
                target.RemoveAt(i);
            }
        }

        for (int i = 0; i < source.Count; i++)
        {
            T incoming = source[i];
            TKey incomingKey = key(incoming);

            int existingIndex = -1;

            for (int j = i; j < target.Count; j++)
            {
                if (EqualityComparer<TKey>.Default.Equals(
                    key(target[j]),
                    incomingKey))
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex == -1)
            {
                target.Insert(i, incoming);
                continue;
            }

            if (existingIndex != i)
            {
                target.Move(existingIndex, i);
            }

            if (signature(target[i]) != signature(incoming))
            {
                target[i] = incoming;
            }
        }
    }
}
