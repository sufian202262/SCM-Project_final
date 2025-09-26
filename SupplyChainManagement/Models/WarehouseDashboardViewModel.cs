using System.Collections.Generic;

namespace SupplyChainManagement.Models
{
    public class WarehouseDashboardViewModel
    {
        public int WarehouseId { get; set; }
        public List<Shipment> InboundShipments { get; set; } = new();
        public List<Inventory> LowStockInventories { get; set; } = new();
        public List<InventoryTransaction> RecentTransactions { get; set; } = new();
        public List<WarehouseTask> MyTasks { get; set; } = new();
        public List<Inventory> ExpiringInventories { get; set; } = new();
        public List<Inventory> DamagedInventories { get; set; } = new();
        public List<Order> OutboundDueOrders { get; set; } = new();

        // Stats
        public int InboundTodayCount { get; set; }
        public int LowStockCount { get; set; }
        public int OutboundDueCount { get; set; }
        public int MyOpenTasksCount { get; set; }
    }
}
