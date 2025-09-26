using System;
using System.ComponentModel.DataAnnotations;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Models
{
    public class Shipment
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }

        [Required]
        public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

        [StringLength(100)]
        public string? Courier { get; set; }

        [StringLength(100)]
        public string? TrackingNumber { get; set; }
    }
}
