using System;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Models
{
    public enum WarehouseTaskType
    {
        Putaway = 1,
        Pick = 2,
        CycleCount = 3
    }

    public enum WarehouseTaskStatus
    {
        Open = 1,
        InProgress = 2,
        Done = 3
    }

    public class WarehouseTask
    {
        public int Id { get; set; }
        public int WarehouseId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string? Bin { get; set; }
        public WarehouseTaskType Type { get; set; }
        public WarehouseTaskStatus Status { get; set; } = WarehouseTaskStatus.Open;
        public DateTime? DueDate { get; set; }
        public string? AssignedToUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Warehouse? Warehouse { get; set; }
        public Product? Product { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }
    }
}
