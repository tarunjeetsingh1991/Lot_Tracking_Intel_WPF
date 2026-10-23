using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LotTrackingWpf.Data;
using LotTrackingWpf.Models;
using LotTrackingWpf.Services;
using LotTrackingWpf.ViewModels;

namespace LotTrackingWpf;

public partial class MainWindow : Window
{
    public bool LogoutRequested { get; private set; }

    private readonly Database database;
    private readonly UserAccount currentUser;

    private PipelineService? pipeline;
    private DispatcherTimer? timer;
    private DispatcherTimer? progressTimer;
    private bool refreshing;

    public MainWindow(Database database, UserAccount currentUser)
    {
        this.database = database;
        this.currentUser = currentUser;

        InitializeComponent();
        UserLabel.Text = $"Signed in: {currentUser.Username} ({currentUser.Role})";

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            timer?.Stop();
            progressTimer?.Stop();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var inventory = new InventoryService(database);
            pipeline = new PipelineService(database, inventory);

            await pipeline.InitializeAsync();

            var vm = new MainViewModel(inventory, pipeline);
            DataContext = vm;
            await vm.Refresh();

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            timer.Tick += async (_, _) =>
            {
                if (refreshing)
                    return;

                refreshing = true;
                try
                {
                    await vm.Refresh();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Refresh failed: " + ex.Message);
                }
                finally
                {
                    refreshing = false;
                }
            };
            timer.Start();

            // Update progress bars frequently, without additional database queries.
            progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            progressTimer.Tick += (_, _) =>
            {
                if (!refreshing)
                    vm.UpdatePlantProgress();
            };
            progressTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Pipeline initialization failed.\n\n" + ex.Message,
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        LogoutRequested = true;
        Close();
    }

    private void SelectWafer_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm ||
            sender is not Button button ||
            button.Tag is not long waferId)
            return;

        if (vm.AssignmentLot == null)
        {
            MessageBox.Show("Select a lot first.");
            return;
        }

        if (vm.AssignmentLot.ProcessingStarted ||
            vm.AssignmentLot.Status is not ("CREATED" or "READY_FOR_PROCESSING"))
        {
            MessageBox.Show("This lot is not available for assignment.");
            return;
        }

        var selected = ParseIds(vm.WaferIds);

        var next = vm.Wafers
            .Where(w => w.LotId == null &&
                        w.Status == "CREATED" &&
                        !selected.Contains(w.Id))
            .OrderBy(w => WaferNumber(w.WaferCode))
            .ThenBy(w => w.Id)
            .FirstOrDefault();

        if (next == null || next.Id != waferId)
        {
            MessageBox.Show("FIFO required. Select the next available wafer.");
            return;
        }

        if (selected.Count + vm.AssignmentLot.TotalWafers >= 24)
        {
            MessageBox.Show("A lot can contain a maximum of 24 wafers.");
            return;
        }

        selected.Add(waferId);
        vm.WaferIds = string.Join(", ", selected);
    }

    private void ClearWaferSelection_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.WaferIds = string.Empty;
    }

    private static List<long> ParseIds(string? text)
    {
        var ids = new List<long>();
        foreach (string part in (text ?? "").Split(
            new[] { ',', ';', ' ', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(part, out long id) && !ids.Contains(id))
                ids.Add(id);
        }
        return ids;
    }

    private static long WaferNumber(string? code)
    {
        if (code != null &&
            code.StartsWith("WAFER", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(code.Substring(5), out long number) &&
            number > 0)
            return number;

        return long.MaxValue;
    }
}
