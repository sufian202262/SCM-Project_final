using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupplyChainManagement.Models
{
    public class Inventory
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product Product { get; set; }

        [Required]
        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityOnHand { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int DamagedQuantity { get; set; } = 0;

        [Required]
        [Range(0, int.MaxValue)]
        public int ReorderLevel { get; set; } = 10;

        [StringLength(50)]
        public string? Aisle { get; set; }

        [StringLength(50)]
        public string? Shelf { get; set; }

        [StringLength(50)]
        public string? Bin { get; set; }

        [NotMapped]
        public string Location => $"{Aisle ?? "A"}-{Shelf ?? "1"}-{Bin ?? "1"}";

        [NotMapped]
        public bool NeedsReorder => AvailableQuantity <= ReorderLevel;

        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [NotMapped]
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.UtcNow.Date;

        [NotMapped]
        public int AvailableQuantity => Math.Max(0, QuantityOnHand - DamagedQuantity);
    }
}
