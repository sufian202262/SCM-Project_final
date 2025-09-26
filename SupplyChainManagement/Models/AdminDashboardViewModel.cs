using System;
using System.Collections.Generic;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Models
{
    public class AdminDashboardViewModel
    {
        // Headline metrics
        public int TotalUsers { get; set; }
        public int TotalSuppliers { get; set; }
        public int TotalWarehouses { get; set; }
        public int TotalProducts { get; set; }

        public int OrdersToday { get; set; }
        public int OrdersPending { get; set; }
        public int OrdersApproved { get; set; }
        public int OrdersSentToSupplier { get; set; }
        public int OrdersProcessing { get; set; }
        public int OrdersShipped { get; set; }
        public int OrdersDelivered { get; set; }

        public int ShipmentsInTransit { get; set; }
        public int ShipmentsDelivered { get; set; }

        public int LowStockCount { get; set; }
        public int DamagedItemsCount { get; set; }

        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueAllTime { get; set; }

        // Lists
        public List<Order> RecentOrders { get; set; } = new();
        public List<Shipment> RecentShipments { get; set; } = new();
        public List<Inventory> LowStockTop { get; set; } = new();
        public List<Product> TopProducts { get; set; } = new();
    }
}
