using System;
using System.Collections.Generic;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Models
{
    public class SupplierDashboardViewModel
    {
        public string SupplierName { get; set; } = string.Empty;
        public int SupplierId { get; set; }

        // Orders
        public List<Order> RecentOrders { get; set; } = new();
        public int OrdersReceivedCount { get; set; }
        public int OrdersConfirmedCount { get; set; }
        public int OrdersProcessingCount { get; set; }
        public int OrdersShippedCount { get; set; }
        public int OrdersDeliveredCount { get; set; }

        // Shipments
        public List<Shipment> RecentShipments { get; set; } = new();

        // Products
        public int ActiveProductsCount { get; set; }
        public List<Product> FeaturedProducts { get; set; } = new();

        // Finance placeholder
        public decimal OutstandingInvoicesTotal { get; set; }

        // Aggregates
        public int TotalSales { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
