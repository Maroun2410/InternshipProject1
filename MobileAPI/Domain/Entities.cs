using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileAPI.Domain;

// Farm is the anchor for worker scoping
public class Farm : BaseEntity, IOwnerScoped
{
    [Required] public Guid OwnerId { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(400)] public string? LocationText { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal? AreaHa { get; set; }
}

// For now, link Planting directly to Farm (we can add Plots later)
public class Planting : BaseEntity, IOwnerScoped
{
    [Required] public Guid OwnerId { get; set; }
    [Required] public Guid FarmId { get; set; }
    [Required, MaxLength(200)] public string CropName { get; set; } = string.Empty;
    public DateTime PlantDate { get; set; }
    public DateTime? ExpectedHarvestDate { get; set; }
}

public class Harvest : BaseEntity, IOwnerScoped
{
    [Required] public Guid OwnerId { get; set; }
    [Required] public Guid PlantingId { get; set; }
    public DateTime Date { get; set; }
    [Column(TypeName = "decimal(14,3)")] public decimal QuantityKg { get; set; }
    [MaxLength(200)] public string? Notes { get; set; }
}

public class Equipment : BaseEntity, IOwnerScoped
{
    [Required] public Guid OwnerId { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Available;
    public Guid? FarmId { get; set; } // null => owner-wide, else tied to a farm
}

public class TaskItem : BaseEntity, IOwnerScoped
{
    [Required] public Guid OwnerId { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    public Guid? FarmId { get; set; }
    public DateTime? DueDate { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Todo;
}

// Which farms a Worker (Identity user) can see
public class WorkerFarmAssignment : BaseEntity
{
    [Required] public Guid OwnerId { get; set; }        // employer owner
    [Required] public Guid WorkerUserId { get; set; }   // Identity user id for the worker
    [Required] public Guid FarmId { get; set; }         // farm they can see
}
