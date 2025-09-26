using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace SupplyChainManagement.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? SKU { get; set; }

        [StringLength(50)]
        public string? Barcode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 9999999999999.99)]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 9999999999999.99)]
        public decimal? CostPrice { get; set; }

        [Range(0, int.MaxValue)]
        public int ReorderLevel { get; set; } = 10;

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; } = 0;

        [StringLength(20)]
        public string? UnitOfMeasure { get; set; } = "pcs";

        public bool IsActive { get; set; } = true;

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        // Navigation properties
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

        // Computed property for total quantity across all warehouses
        [NotMapped]
        public int TotalInStock => Inventories?.Sum(i => i.QuantityOnHand) ?? 0;

        // Computed property to check if stock is low
        [NotMapped]
        public bool IsLowStock => TotalInStock <= ReorderLevel;
    }
}
