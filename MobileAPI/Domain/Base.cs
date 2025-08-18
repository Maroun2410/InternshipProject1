using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileAPI.Domain;

public interface IOwnerScoped
{
    Guid OwnerId { get; set; }
}

public abstract class BaseEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public enum EquipmentStatus { Available = 0, InUse = 1, Maintenance = 2 }
public enum TaskStatus { Todo = 0, InProgress = 1, Done = 2 }
