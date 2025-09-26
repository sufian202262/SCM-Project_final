using System;
using System.ComponentModel.DataAnnotations;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Models
{
    public enum InvoiceStatus
    {
        Unpaid = 0,
        PartiallyPaid = 1,
        Paid = 2
    }

    public class Invoice
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;

        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
    }
}
