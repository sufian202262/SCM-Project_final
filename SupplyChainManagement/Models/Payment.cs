using System;
using System.ComponentModel.DataAnnotations;

namespace SupplyChainManagement.Models
{
    public enum PaymentMethod
    {
        Bkash = 0,
        Nagad = 1,
        Rocket = 2,
        Card = 3,
        BankTransfer = 4,
        Cash = 5,
        MobileWallet = 6
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Authorized = 1,
        Captured = 2,
        Failed = 3,
        Cancelled = 4,
        Refunded = 5,
        PartiallyRefunded = 6
    }

    public class Payment
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;

        [Required]
        [Range(0, 9999999999999.99)]
        public decimal Amount { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "BDT";

        [Required]
        public PaymentMethod Method { get; set; }

        [Required]
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        // Gateway info
        [StringLength(50)]
        public string? Gateway { get; set; } // e.g., SSLCommerz, bKash

        [StringLength(100)]
        public string? TransactionId { get; set; }

        [StringLength(100)]
        public string? ProviderRef { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public string? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Optional: raw payload from gateway for audit/debug
        public string? RawPayload { get; set; }
    }
}
