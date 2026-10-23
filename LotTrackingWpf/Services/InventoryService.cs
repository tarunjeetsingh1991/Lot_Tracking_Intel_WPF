
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using LotTrackingWpf.Data;
using LotTrackingWpf.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;
using Database = LotTrackingWpf.Data.Database;

namespace LotTrackingWpf.Services;

public sealed class InventoryService
{
    private readonly Database db;

    public InventoryService(Database db)
    {
        this.db = db;
    }

    public async Task<List<Supplier>> Suppliers()
    {
        await using var c = db.Open();
        return await c.Suppliers.AsNoTracking()
            .OrderBy(x => x.Id).ToListAsync();
    }

    public async Task<List<Wafer>> Wafers()
    {
        await using var c = db.Open();

        return await (
            from w in c.Wafers.AsNoTracking()
            join s in c.Suppliers.AsNoTracking()
                on w.SupplierId equals s.Id
            orderby w.Id
            select new Wafer
            {
                Id = w.Id,
                WaferCode = w.WaferCode,
                WaferType = w.WaferType,
                SupplierId = w.SupplierId,
                SupplierName = s.SupplierName,
                LotId = w.LotId,
                Status = w.Status,
                CreatedAt = w.CreatedAt
            }).ToListAsync();
    }

    public async Task<List<Lot>> Lots()
    {
        await using var c = db.Open();
        return await c.Lots.AsNoTracking()
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<List<Plant>> Plants()
    {
        await using var c = db.Open();

        return await (
            from p in c.Plants.AsNoTracking()
            join l in c.Lots.AsNoTracking()
                on p.CurrentLotId equals (long?)l.Id
                into matches
            from l in matches.DefaultIfEmpty()
            orderby p.PlantNumber
            select new Plant
            {
                PlantNumber = p.PlantNumber,
                PlantName = p.PlantName,
                BaseProcessingTimeMs = p.BaseProcessingTimeMs,
                Occupied = p.Occupied,
                CurrentLotId = p.CurrentLotId,
                CurrentLotCode = l == null ? null : l.LotCode
            }).ToListAsync();
    }

    public async Task<List<ProcessLog>> Logs()
    {
        await using var c = db.Open();
        return await c.ProcessLogs.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(1000).ToListAsync();
    }

    private static void AddLog(
        LotTrackingDbContext c,
        string type,
        string code,
        string message,
        int? plant = null)
    {
        c.ProcessLogs.Add(new ProcessLog
        {
            EntityType = type,
            EntityCode = code,
            Message = message,
            PlantNumber = plant,
            LogTime = DateTime.Now
        });
    }

    public async Task Log(
        string type,
        string code,
        string message,
        int? plant = null)
    {
        await using var c = db.Open();
        AddLog(c, type, code, message, plant);
        await c.SaveChangesAsync();
    }

    private static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{name} is required.");

        return value.Trim();
    }

    private static async Task CheckSupplier(
        LotTrackingDbContext c, long id)
    {
        if (!await c.Suppliers.AnyAsync(x => x.Id == id))
            throw new InvalidOperationException(
                "Supplier not found.");
    }

    private static long WaferNumber(string? code)
    {
        if (code != null &&
            code.StartsWith(
                "WAFER", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(code.Substring(5), out long n) &&
            n > 0)
        {
            return n;
        }

        return long.MaxValue;
    }

    private static async Task Retry(Func<Task> action)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (MySqlException ex) when (
                (ex.Number == 1213 || ex.Number == 1205) &&
                attempt < 3)
            {
                await Task.Delay(100 * attempt);
            }
        }
    }

    private static async Task LockWaferSequence(
        LotTrackingDbContext c)
    {
        var connection = c.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.Transaction =
            c.Database.CurrentTransaction!.GetDbTransaction();

        command.CommandText =
            "SELECT next_value FROM wafer_sequences " +
            "WHERE sequence_name = 'WAFER' FOR UPDATE";

        var value = await command.ExecuteScalarAsync();

        if (value == null || value == DBNull.Value)
            throw new InvalidOperationException(
                "Initialize wafer_sequences before using inventory.");
    }

    // SUPPLIERS

    public async Task AddSupplier(
        string name, string email, string location)
    {
        name = Required(name, "Supplier name");

        await using var c = db.Open();

        c.Suppliers.Add(new Supplier
        {
            SupplierName = name,
            ContactEmail = email?.Trim() ?? "",
            Location = location?.Trim() ?? "",
            CreatedAt = DateTime.Now
        });

        AddLog(c, "SUPPLIER", name, "Supplier created.");
        await c.SaveChangesAsync();
    }

    public async Task UpdateSupplier(
        long id, string name, string email, string location)
    {
        name = Required(name, "Supplier name");

        await using var c = db.Open();

        var s = await c.Suppliers.FindAsync(id)
            ?? throw new InvalidOperationException(
                "Supplier not found.");

        s.SupplierName = name;
        s.ContactEmail = email?.Trim() ?? "";
        s.Location = location?.Trim() ?? "";

        AddLog(c, "SUPPLIER", name, "Supplier updated.");
        await c.SaveChangesAsync();
    }

    public async Task DeleteSupplier(long id)
    {
        await using var c = db.Open();
        await using var tx = await c.Database.BeginTransactionAsync();

        var s = await c.Suppliers.FindAsync(id)
            ?? throw new InvalidOperationException(
                "Supplier not found.");

        if (await c.Wafers.AnyAsync(w => w.SupplierId == id))
            throw new InvalidOperationException(
                "Cannot delete supplier with existing wafers.");

        c.Suppliers.Remove(s);
        AddLog(c, "SUPPLIER", s.SupplierName, "Supplier deleted.");

        await c.SaveChangesAsync();
        await tx.CommitAsync();
    }

    // SINGLE WAFER

    public async Task AddWafer(
        string code, string type, long supplierId)
    {
        code = Required(code, "Wafer code");
        type = Required(type, "Wafer type");

        await Retry(async () =>
        {
            await using var c = db.Open();
            await using var tx = await c.Database.BeginTransactionAsync();

            await LockWaferSequence(c);
            await CheckSupplier(c, supplierId);

            if (await c.Wafers.AnyAsync(w => w.WaferCode == code))
                throw new InvalidOperationException(
                    "Wafer code already exists.");

            c.Wafers.Add(new Wafer
            {
                WaferCode = code,
                WaferType = type,
                SupplierId = supplierId,
                Status = "CREATED",
                CreatedAt = DateTime.Now
            });

            AddLog(c, "WAFER", code, "Wafer created.");
            await c.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }

    // BULK WAFERS: WAFER1, WAFER2, ...

    public async Task BulkWafers(
        string type, long supplierId, int count)
    {
        type = Required(type, "Wafer type");

        if (count < 1 || count > 500)
            throw new InvalidOperationException(
                "Bulk count must be between 1 and 500.");

        await Retry(async () =>
        {
            await using var c = db.Open();
            await using var tx =
                await c.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted);

            await CheckSupplier(c, supplierId);
            await LockWaferSequence(c);

            var connection = c.Database.GetDbConnection();

            await using var command = connection.CreateCommand();
            command.Transaction = tx.GetDbTransaction();
            command.CommandText =
                "SELECT next_value FROM wafer_sequences " +
                "WHERE sequence_name = 'WAFER'";

            long next = Convert.ToInt64(
                await command.ExecuteScalarAsync());

            var existing = await c.Wafers.AsNoTracking()
                .Where(w => w.WaferCode.StartsWith("WAFER"))
                .Select(w => w.WaferCode)
                .ToListAsync();

            var used = existing
                .Select(WaferNumber)
                .Where(n => n != long.MaxValue)
                .ToHashSet();

            long first = next;

            while (true)
            {
                long last = checked(first + count - 1L);
                long? collision = null;

                for (long n = first; n <= last; n++)
                {
                    if (used.Contains(n))
                    {
                        collision = n;
                        break;
                    }
                }

                if (collision == null)
                    break;

                first = checked(collision.Value + 1);
            }

            long following = checked(first + count);

            await using (var update = connection.CreateCommand())
            {
                update.Transaction = tx.GetDbTransaction();
                update.CommandText =
                    "UPDATE wafer_sequences " +
                    "SET next_value = @next " +
                    "WHERE sequence_name = 'WAFER'";

                var parameter = update.CreateParameter();
                parameter.ParameterName = "@next";
                parameter.Value = following;
                update.Parameters.Add(parameter);

                await update.ExecuteNonQueryAsync();
            }

            for (int i = 0; i < count; i++)
            {
                string code = $"WAFER{first + i}";

                c.Wafers.Add(new Wafer
                {
                    WaferCode = code,
                    WaferType = type,
                    SupplierId = supplierId,
                    Status = "CREATED",
                    CreatedAt = DateTime.Now
                });

                AddLog(c, "WAFER", code, "Bulk wafer created.");
            }

            await c.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }

    public async Task UpdateWafer(
        long id, string code, string type, long supplierId)
    {
        code = Required(code, "Wafer code");
        type = Required(type, "Wafer type");

        await Retry(async () =>
        {
            await using var c = db.Open();
            await using var tx = await c.Database.BeginTransactionAsync();

            await LockWaferSequence(c);
            await CheckSupplier(c, supplierId);

            var w = await c.Wafers.FindAsync(id)
                ?? throw new InvalidOperationException(
                    "Wafer not found.");

            if (w.LotId != null)
                throw new InvalidOperationException(
                    "Assigned wafers cannot be edited.");

            if (await c.Wafers.AnyAsync(x =>
                x.Id != id && x.WaferCode == code))
                throw new InvalidOperationException(
                    "Wafer code already exists.");

            w.WaferCode = code;
            w.WaferType = type;
            w.SupplierId = supplierId;

            AddLog(c, "WAFER", code, "Wafer updated.");
            await c.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }

    public async Task DeleteWafer(long id)
    {
        await Retry(async () =>
        {
            await using var c = db.Open();
            await using var tx = await c.Database.BeginTransactionAsync();

            await LockWaferSequence(c);

            var w = await c.Wafers.FindAsync(id)
                ?? throw new InvalidOperationException(
                    "Wafer not found.");

            if (w.LotId != null)
                throw new InvalidOperationException(
                    "Assigned wafers cannot be deleted.");

            c.Wafers.Remove(w);
            AddLog(c, "WAFER", w.WaferCode, "Wafer deleted.");

            await c.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }

    // LOTS

    public async Task AddLot(string code)
    {
        code = Required(code, "Lot code");

        await using var c = db.Open();

        if (await c.Lots.AnyAsync(x => x.LotCode == code))
            throw new InvalidOperationException(
                "Lot code already exists.");

        c.Lots.Add(new Lot
        {
            LotCode = code,
            Status = "CREATED",
            TotalWafers = 0,
            ProcessingStarted = false,
            CreatedAt = DateTime.Now
        });

        AddLog(c, "LOT", code, "Lot created.");
        await c.SaveChangesAsync();
    }

    public async Task UpdateLot(long id, string code)
    {
        code = Required(code, "Lot code");

        await using var c = db.Open();

        var lot = await c.Lots.FindAsync(id)
            ?? throw new InvalidOperationException("Lot not found.");

        if (lot.ProcessingStarted)
            throw new InvalidOperationException(
                "Started lots cannot be updated.");

        if (await c.Lots.AnyAsync(x =>
            x.Id != id && x.LotCode == code))
            throw new InvalidOperationException(
                "Lot code already exists.");

        lot.LotCode = code;

        AddLog(c, "LOT", code, "Lot updated.");
        await c.SaveChangesAsync();
    }

    public async Task DeleteLot(long id)
    {
        await Retry(async () =>
        {
            await using var c = db.Open();
            await using var tx =
                await c.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            await LockWaferSequence(c);

            Lot? lot = null;

            await foreach (var item in c.Lots
                .FromSqlInterpolated(
                    $"SELECT * FROM lots WHERE id = {id} FOR UPDATE")
                .AsAsyncEnumerable())
            {
                lot = item;
                break;
            }

            if (lot == null)
                throw new InvalidOperationException("Lot not found.");

            if (lot.ProcessingStarted ||
                lot.Status is "WAITING" or "IN_PROGRESS"
                    or "COMPLETED" or "FAILED")
                throw new InvalidOperationException(
                    "Cannot delete a started or processed lot.");

            if (await c.Plants.AnyAsync(
                p => p.CurrentLotId == id))
                throw new InvalidOperationException(
                    "Lot is currently in a plant.");

            var wafers = await c.Wafers
                .Where(w => w.LotId == id)
                .ToListAsync();

            foreach (var w in wafers)
            {
                w.LotId = null;
                w.Status = "CREATED";
            }

            c.Lots.Remove(lot);

            AddLog(c, "LOT", lot.LotCode,
                "Lot deleted; wafers returned to inventory.");

            await c.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }

    // STRICT FIFO WAFER ASSIGNMENT

    public async Task Assign(
        long lotId, IEnumerable<long> waferIds)
    {
        if (waferIds == null)
            throw new ArgumentNullException(nameof(waferIds));

        long[] requested = waferIds.Distinct().ToArray();

        if (requested.Length == 0)
            throw new InvalidOperationException(
                "Select at least one wafer.");

        if (requested.Length > 24)
            throw new InvalidOperationException(
                "Maximum 24 wafers per lot.");

        await Retry(async () =>
        {
            await using var c = db.Open();

            await using var tx =
                await c.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted);

            // Global lock serializes FIFO assignments.
            await LockWaferSequence(c);

            Lot? lot = null;

            await foreach (var item in c.Lots
                .FromSqlInterpolated(
                    $"SELECT * FROM lots WHERE id = {lotId} FOR UPDATE")
                .AsAsyncEnumerable())
            {
                lot = item;
                break;
            }

            if (lot == null)
                throw new InvalidOperationException("Lot not found.");

            if (lot.ProcessingStarted ||
                lot.Status is not ("CREATED" or
                                  "READY_FOR_PROCESSING"))
                throw new InvalidOperationException(
                    "Lot is not available for assignment.");

            int actualCount = await c.Wafers.CountAsync(
                w => w.LotId == lotId);

            if (actualCount + requested.Length > 24)
                throw new InvalidOperationException(
                    "A lot cannot contain more than 24 wafers.");

            // FIFO is based on wafer-code numbers,
            // not database IDs or lot creation dates.
            var available = await c.Wafers
                .Where(w => w.LotId == null &&
                            w.Status == "CREATED")
                .ToListAsync();

            available = available
                .OrderBy(w => WaferNumber(w.WaferCode))
                .ThenBy(w => w.Id)
                .ToList();

            if (available.Count < requested.Length)
                throw new InvalidOperationException(
                    "Not enough available wafers.");

            var expected = available
                .Take(requested.Length)
                .Select(w => w.Id)
                .ToHashSet();

            if (!expected.SetEquals(requested))
            {
                string next = available[0].WaferCode;

                throw new InvalidOperationException(
                    "FIFO assignment required. " +
                    $"Start with {next} and select consecutive " +
                    "available wafers without skipping any.");
            }

            foreach (var wafer in available
                .Take(requested.Length))
            {
                wafer.LotId = lotId;
                wafer.Status = "ASSIGNED_TO_LOT";
            }

            lot.TotalWafers = actualCount + requested.Length;
            lot.Status = "READY_FOR_PROCESSING";

            AddLog(c, "LOT", lot.LotCode,
                $"FIFO assignment: {requested.Length} wafers. " +
                $"Total: {lot.TotalWafers}.");

            await c.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }

    // PLANT SETTINGS

    public async Task SetPlantTime(
        int number, long milliseconds)
    {
        if (milliseconds < 1 || milliseconds > 600000)
            throw new InvalidOperationException(
                "Plant time must be between 1 and 600000 ms.");

        await using var c = db.Open();

        var plant = await c.Plants.FindAsync(number)
            ?? throw new InvalidOperationException(
                "Plant not found.");

        plant.BaseProcessingTimeMs = milliseconds;

        AddLog(c, "PLANT", number.ToString(),
            $"Plant time updated to {milliseconds} ms.");

        await c.SaveChangesAsync();
    }
}
