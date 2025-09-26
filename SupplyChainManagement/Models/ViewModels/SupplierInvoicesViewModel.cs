using System.Collections.Generic;

namespace SupplyChainManagement.Models.ViewModels
{
    public class SupplierInvoicesViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        public List<Invoice> Invoices { get; set; } = new();
        public List<Order> EligibleOrders { get; set; } = new();

        // Summary
        public decimal TotalInvoiced { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalUnpaid { get; set; }
    }
}
