
using System.ComponentModel.DataAnnotations.Schema;

namespace LotTrackingWpf.Models;

public sealed class Supplier
{
    public long Id { get; set; }

    public string SupplierName { get; set; } = "";

    public string ContactEmail { get; set; } = "";

    public string Location { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}

public sealed class Wafer
{
    public long Id { get; set; }

    public string WaferCode { get; set; } = "";

    public string WaferType { get; set; } = "";

    public long SupplierId { get; set; }

    public string SupplierName { get; set; } = "";

    public long? LotId { get; set; }

    public string Status { get; set; } = "CREATED";

    public DateTime CreatedAt { get; set; }
}

public sealed class Lot
{
    public long Id { get; set; }

    public string LotCode { get; set; } = "";

    public string Status { get; set; } = "CREATED";

    public int CurrentPlantNumber { get; set; }

    public int TotalWafers { get; set; }

    public bool ProcessingStarted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public sealed class Plant
{
    public int PlantNumber { get; set; }

    public string PlantName { get; set; } = "";

    public long BaseProcessingTimeMs { get; set; }

    public bool Occupied { get; set; }

    public long? CurrentLotId { get; set; }

    public string? CurrentLotCode { get; set; }

    [NotMapped]
    public double ProcessingProgress { get; set; }
}

public sealed class ProcessLog
{
    public long Id { get; set; }

    public DateTime LogTime { get; set; }

    public string EntityType { get; set; } = "";

    public string EntityCode { get; set; } = "";

    public int? PlantNumber { get; set; }

    public string Message { get; set; } = "";
}
