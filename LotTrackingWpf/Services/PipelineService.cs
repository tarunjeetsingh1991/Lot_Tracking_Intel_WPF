
using LotTrackingWpf.Data;
using LotTrackingWpf.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Database = LotTrackingWpf.Data.Database;
namespace LotTrackingWpf.Services;

public sealed class PipelineService : IAsyncDisposable
{
    private const int StationCount = 10;

    private readonly Database database;
    private readonly InventoryService inventory;

    private readonly SemaphoreSlim[] stations =
        Enumerable.Range(0, StationCount)
            .Select(_ => new SemaphoreSlim(1, 1))
            .ToArray();

    private readonly Channel<long> queue =
        Channel.CreateUnbounded<long>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

    private readonly ConcurrentDictionary<long, byte> submitted = new();

    // Progress is transient UI state; no database column is required.
    private sealed record PlantOperation(long LotId, long StartedAt, long DurationMs);
    private readonly ConcurrentDictionary<int, PlantOperation> plantOperations = new();

    public double GetPlantProgress(int plantNumber, long? currentLotId)
    {
        if (!currentLotId.HasValue ||
            !plantOperations.TryGetValue(plantNumber, out var operation) ||
            operation.LotId != currentLotId.Value)
            return 0;

        if (operation.DurationMs <= 0)
            return 0;

        double elapsedMs = Stopwatch.GetElapsedTime(operation.StartedAt).TotalMilliseconds;
        double percent = Math.Clamp(elapsedMs / operation.DurationMs * 100.0, 0, 100);
        return Math.Floor(percent / 25.0) * 25.0;
    }

    private readonly Dictionary<long, Task> activeTasks = new();
    private readonly object tasksLock = new();

    private readonly CancellationTokenSource cancellation = new();
    private readonly SemaphoreSlim submissionLock = new(1, 1);
    private readonly SemaphoreSlim initializationLock = new(1, 1);

    private readonly Task dispatcher;
    private readonly object stopLock = new();

    private Task? stopTask;
    private bool initialized;
    private bool stopping;
    private bool disposed;

    public PipelineService(
        Database database,
        InventoryService inventory)
    {
        this.database = database;
        this.inventory = inventory;

        dispatcher = Task.Run(DispatchAsync);
    }

    // Called once when MainWindow starts.
    // Assumes this application is the only pipeline controller.
    public async Task InitializeAsync()
    {
        await initializationLock.WaitAsync();

        try
        {
            if (initialized)
            {
                return;
            }

            await RecoverInterruptedLotsAsync();

            initialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private async Task RecoverInterruptedLotsAsync()
    {
        await using var db = database.Open();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        var interruptedLots = await db.Lots
            .Where(l =>
                l.ProcessingStarted &&
                l.Status != "COMPLETED" &&
                l.Status != "FAILED")
            .ToListAsync();

        var lotIds = interruptedLots
            .Select(l => l.Id)
            .ToArray();

        foreach (var lot in interruptedLots)
        {
            lot.Status = "FAILED";
            lot.CurrentPlantNumber = 0;

            db.ProcessLogs.Add(new ProcessLog
            {
                EntityType = "LOT",
                EntityCode = lot.LotCode,
                Message = "Processing interrupted; lot marked FAILED.",
                LogTime = DateTime.Now
            });
        }

        if (lotIds.Length > 0)
        {
            await db.Wafers
                .Where(w =>
                    w.LotId.HasValue &&
                    lotIds.Contains(w.LotId.Value))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(w => w.Status, "FAILED"));
        }

        var occupiedPlants = await db.Plants
            .Where(p =>
                p.Occupied ||
                p.CurrentLotId != null)
            .ToListAsync();

        foreach (var plant in occupiedPlants)
        {
            plant.Occupied = false;
            plant.CurrentLotId = null;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    // Accept a lot into the FIFO queue.

    public async Task Submit(long id)
    {
        await submissionLock.WaitAsync();

        try
        {
            if (stopping || disposed)
                throw new InvalidOperationException(
                    "Pipeline is shutting down.");

            if (!initialized)
                throw new InvalidOperationException(
                    "Pipeline has not been initialized.");

            await using var db = database.Open();

            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            // Serialize submissions across database connections
            // using the same global row used by inventory.
            var connection = db.Database.GetDbConnection();

            await using (var command = connection.CreateCommand())
            {
                command.Transaction =
                    transaction.GetDbTransaction();

                command.CommandText =
                    "SELECT next_value FROM wafer_sequences " +
                    "WHERE sequence_name = 'WAFER' FOR UPDATE";

                var result = await command.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException(
                        "Wafer sequence has not been initialized.");
            }

            var lot = await db.Lots.FindAsync(id)
                ?? throw new InvalidOperationException(
                    "Lot not found.");

            if (lot.ProcessingStarted ||
                lot.Status != "READY_FOR_PROCESSING")
            {
                throw new InvalidOperationException(
                    "Lot is not ready for processing.");
            }

            if (lot.TotalWafers < 1 || lot.TotalWafers > 24)
            {
                throw new InvalidOperationException(
                    "A lot must contain 1 to 24 wafers.");
            }

            var wafers = await db.Wafers
                .AsNoTracking()
                .Select(w => new
                {
                    w.Id,
                    w.WaferCode,
                    w.LotId,
                    w.Status
                })
                .ToListAsync();

            long GetNumber(string code)
            {
                if (!code.StartsWith(
                        "WAFER",
                        StringComparison.OrdinalIgnoreCase) ||
                    !long.TryParse(
                        code.Substring(5),
                        out long number) ||
                    number <= 0)
                {
                    throw new InvalidOperationException(
                        $"Invalid wafer code: {code}. " +
                        "FIFO processing requires WAFER1, WAFER2, etc.");
                }

                return number;
            }

            var selectedWafers = wafers
                .Where(w => w.LotId == id)
                .ToList();

            if (selectedWafers.Count != lot.TotalWafers)
            {
                throw new InvalidOperationException(
                    "Lot wafer count does not match inventory.");
            }

            long firstNumber = selectedWafers
                .Min(w => GetNumber(w.WaferCode));

            // Every wafer with a lower number must already
            // belong to a lot that has started processing,
            // or to a lot that has completed processing.
            var earlierWafers = wafers
                .Where(w => GetNumber(w.WaferCode) < firstNumber)
                .ToList();

            var earlierLotIds = earlierWafers
                .Where(w => w.LotId.HasValue)
                .Select(w => w.LotId!.Value)
                .Distinct()
                .ToArray();

            var earlierLots = await db.Lots
                .AsNoTracking()
                .Where(l => earlierLotIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id);

            foreach (var wafer in earlierWafers
                .OrderBy(w => GetNumber(w.WaferCode)))
            {
                if (!wafer.LotId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"FIFO violation: {wafer.WaferCode} " +
                        "has not been assigned to a lot. " +
                        $"Cannot start {lot.LotCode}.");
                }

                if (!earlierLots.TryGetValue(
                        wafer.LotId.Value,
                        out var earlierLot))
                {
                    throw new InvalidOperationException(
                        $"FIFO violation: lot for " +
                        $"{wafer.WaferCode} was not found.");
                }

                if (!earlierLot.ProcessingStarted)
                {
                    throw new InvalidOperationException(
                        $"FIFO violation: {wafer.WaferCode} " +
                        $"belongs to {earlierLot.LotCode}. " +
                        $"Start {earlierLot.LotCode} before " +
                        $"{lot.LotCode}.");
                }

                if (earlierLot.Status == "FAILED")
                {
                    throw new InvalidOperationException(
                        $"FIFO blocked: {earlierLot.LotCode} " +
                        $"containing {wafer.WaferCode} failed. " +
                        "Resolve the failed lot before continuing.");
                }
            }

            if (!submitted.TryAdd(id, 0))
            {
                throw new InvalidOperationException(
                    "Lot is already submitted.");
            }

            bool committed = false;

            try
            {
                lot.ProcessingStarted = true;
                lot.Status = "WAITING";

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                committed = true;

                if (!queue.Writer.TryWrite(id))
                {
                    // The database now says WAITING, but the
                    // dispatcher cannot accept the lot.
                    // Do not silently roll back a committed
                    // submission while other workers may run.
                    await MarkFailedAsync(id);

                    throw new InvalidOperationException(
                        "Processing queue is closed. " +
                        "Lot has been marked FAILED.");
                }

                await SafeLogAsync(
                    lot.LotCode,
                    "Lot submitted in wafer-number FIFO order.");
            }
            catch
            {
                if (!committed)
                    await transaction.RollbackAsync();

                submitted.TryRemove(id, out _);
                throw;
            }
        }
        finally
        {
            submissionLock.Release();
        }
    }


    // One reader ensures FIFO admission to Plant 1.
    private async Task DispatchAsync()
    {
        try
        {
            await foreach (
                long id in queue.Reader.ReadAllAsync(
                    cancellation.Token))
            {
                await stations[0].WaitAsync(
                    cancellation.Token);

                // Register the task before allowing it to run.
                var startGate =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                Task worker = Task.Run(async () =>
                {
                    await startGate.Task;
                    await ProcessAsync(id);
                });

                lock (tasksLock)
                {
                    activeTasks[id] = worker;
                }

                _ = worker.ContinueWith(
                    completedTask =>
                    {
                        lock (tasksLock)
                        {
                            activeTasks.Remove(id);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                startGate.SetResult(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
    }

    private async Task ProcessAsync(long id)
    {
        // Plant 1 semaphore is acquired by the dispatcher.
        var ownedStations = new HashSet<int> { 1 };

        string lotCode = id.ToString();
        bool completed = false;

        try
        {
            await using (var db = database.Open())
            {
                lotCode = await db.Lots
                    .Where(l => l.Id == id)
                    .Select(l => l.LotCode)
                    .FirstOrDefaultAsync(cancellation.Token)
                    ?? lotCode;
            }

            await OccupyAsync(id, 1);

            for (int station = 1;
                 station <= StationCount;
                 station++)
            {
                cancellation.Token.ThrowIfCancellationRequested();

                await SafeLogAsync(
                    lotCode,
                    $"Processing at Plant {station}.",
                    station);

                long durationMs =
                    await GetProcessingDurationAsync(
                        id,
                        station);

                plantOperations[station] = new PlantOperation(
                    id, Stopwatch.GetTimestamp(), durationMs);

                await Task.Delay(
                    TimeSpan.FromMilliseconds(durationMs),
                    cancellation.Token);

                if (station == StationCount)
                {
                    await CompleteLotAsync(id);
                    completed = true;

                    await SafeLogAsync(
                        lotCode,
                        "All 10 plants completed.",
                        station);

                    break;
                }

                int next = station + 1;

                await SafeLogAsync(
                    lotCode,
                    $"Waiting for Plant {next}.",
                    station);

                await stations[next - 1].WaitAsync(
                    cancellation.Token);

                ownedStations.Add(next);

                // Transfer occupancy to the next plant.
                await OccupyAsync(id, next);

                // Release the previous plant only after
                // the next plant is successfully occupied.
                await ReleaseStationAsync(
                    id,
                    station,
                    ownedStations);
            }
        }
        catch (OperationCanceledException)
        {
            await SafeLogAsync(
                lotCode,
                "Processing cancelled during shutdown.");
        }
        catch (Exception ex)
        {
            await SafeLogAsync(
                lotCode,
                "Processing failed: " + ex.Message);
        }
        finally
        {
            // Mark the lot failed before releasing its
            // remaining station ownership.
            if (!completed)
            {
                try
                {
                    await MarkFailedAsync(id);
                }
                catch (Exception ex)
                {
                    await SafeLogAsync(
                        lotCode,
                        "Unable to mark lot FAILED: " + ex.Message);
                }
            }

            foreach (int station in ownedStations.ToArray())
            {
                try
                {
                    await ReleaseStationAsync(
                        id,
                        station,
                        ownedStations);
                }
                catch (Exception ex)
                {
                    await SafeLogAsync(
                        lotCode,
                        $"Plant {station} cleanup error: {ex.Message}",
                        station);
                }
            }

            submitted.TryRemove(id, out _);
        }
    }

    private async Task<long> GetProcessingDurationAsync(
        long lotId,
        int station)
    {
        await using var db = database.Open();

        var plant = await db.Plants.FindAsync(station)
            ?? throw new Exception(
                $"Plant {station} not found.");

        var lot = await db.Lots.FindAsync(lotId)
            ?? throw new Exception("Lot not found.");

        if (plant.BaseProcessingTimeMs <= 0)
        {
            throw new Exception(
                "Plant processing time must be positive.");
        }

        return checked(
            plant.BaseProcessingTimeMs * lot.TotalWafers);
    }

    private async Task OccupyAsync(
        long lotId,
        int station)
    {
        await using var db = database.Open();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        var plant = await db.Plants.FindAsync(station)
            ?? throw new Exception(
                $"Plant {station} not found.");

        var lot = await db.Lots.FindAsync(lotId)
            ?? throw new Exception("Lot not found.");

        if (plant.Occupied &&
            plant.CurrentLotId != lotId)
        {
            throw new InvalidOperationException(
                $"Plant {station} is already occupied.");
        }

        plant.Occupied = true;
        plant.CurrentLotId = lotId;

        lot.Status = "IN_PROGRESS";
        lot.CurrentPlantNumber = station;

        await db.Wafers
            .Where(w => w.LotId == lotId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    w => w.Status, "IN_PROCESS"));

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task ReleaseStationAsync(
        long lotId,
        int station,
        HashSet<int> ownedStations)
    {
        if (!ownedStations.Contains(station))
        {
            return;
        }

        // Only clear occupancy belonging to this lot.
        // The semaphore is released in finally so that
        // an exception cannot strand the station forever.
        try
        {
            await using var db = database.Open();

            await db.Plants
                .Where(p =>
                    p.PlantNumber == station &&
                    p.CurrentLotId == lotId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Occupied, false)
                    .SetProperty(
                        p => p.CurrentLotId, (long?)null));
        }
        finally
        {
            if (plantOperations.TryGetValue(station, out var operation) &&
                operation.LotId == lotId)
            {
                ((ICollection<KeyValuePair<int, PlantOperation>>)plantOperations)
                    .Remove(new KeyValuePair<int, PlantOperation>(station, operation));
            }

            ownedStations.Remove(station);
            stations[station - 1].Release();
        }
    }

    private async Task CompleteLotAsync(long lotId)
    {
        await using var db = database.Open();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        await db.Lots
            .Where(l => l.Id == lotId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    l => l.Status, "COMPLETED")
                .SetProperty(
                    l => l.CurrentPlantNumber, 0)
                .SetProperty(
                    l => l.CompletedAt, DateTime.Now));

        await db.Wafers
            .Where(w => w.LotId == lotId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    w => w.Status, "COMPLETED"));

        await transaction.CommitAsync();
    }

    private async Task MarkFailedAsync(long lotId)
    {
        await using var db = database.Open();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        int updated = await db.Lots
            .Where(l =>
                l.Id == lotId &&
                l.Status != "COMPLETED")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.Status, "FAILED")
                .SetProperty(l => l.CurrentPlantNumber, 0));

        if (updated > 0)
        {
            await db.Wafers
                .Where(w => w.LotId == lotId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(w => w.Status, "FAILED"));
        }

        await transaction.CommitAsync();
    }

    private async Task SafeLogAsync(
        string code,
        string message,
        int? plant = null)
    {
        try
        {
            await inventory.Log(
                "LOT",
                code,
                message,
                plant);
        }
        catch
        {
            // Logging failures must not interrupt cleanup.
        }
    }

    public Task StopAsync()
    {
        lock (stopLock)
        {
            return stopTask ??= StopCoreAsync();
        }
    }

    private async Task StopCoreAsync()
    {
        await submissionLock.WaitAsync();

        try
        {
            stopping = true;

            queue.Writer.TryComplete();
            cancellation.Cancel();
        }
        finally
        {
            submissionLock.Release();
        }

        try
        {
            await dispatcher;
        }
        catch (OperationCanceledException)
        {
        }

        Task[] workers;

        lock (tasksLock)
        {
            workers = activeTasks.Values.ToArray();
        }

        await Task.WhenAll(workers);

        // The pipeline is now stopped.
        // Mark queued or interrupted lots FAILED.
        await RecoverInterruptedLotsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAsync();

        disposed = true;

        cancellation.Dispose();
        submissionLock.Dispose();
        initializationLock.Dispose();

        foreach (var station in stations)
        {
            station.Dispose();
        }
    }
}
