using System;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Models
{
    public enum InventoryTransactionType
    {
        Receipt = 1,
        Issue = 2,
        Adjustment = 3,
        TransferIn = 4,
        TransferOut = 5
    }

    public class InventoryTransaction
    {
        public int Id { get; set; }
        public int WarehouseId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; } // + for receipt, - for issue
        public InventoryTransactionType Type { get; set; }
        public string? ReferenceType { get; set; } // e.g., "Shipment", "Order", "Manual"
        public int? ReferenceId { get; set; }
        public string? PerformedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }

        public Warehouse? Warehouse { get; set; }
        public Product? Product { get; set; }
        public ApplicationUser? PerformedByUser { get; set; }
    }
}
