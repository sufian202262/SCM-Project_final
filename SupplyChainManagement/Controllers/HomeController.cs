using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplyChainManagement.Data;
using SupplyChainManagement.Models;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole(UserRoles.Admin))
        {
            var today = DateTime.UtcNow.Date;

            var totalUsers = await _context.Users.CountAsync();
            var totalSuppliers = await _context.Suppliers.CountAsync();
            var totalWarehouses = await _context.Warehouses.CountAsync();
            var totalProducts = await _context.Products.CountAsync();

            var ordersToday = await _context.Orders.CountAsync(o => o.CreatedAt >= today);
            var ordersPending = await _context.Orders.CountAsync(o => o.Status == OrderStatus.PendingApproval);
            var ordersApproved = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Approved);
            var ordersProcessing = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Processing);
            var ordersSentToSupplier = await _context.Orders.CountAsync(o => o.Status == OrderStatus.SentToSupplier);
            var ordersShipped = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Shipped);
            var ordersDelivered = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Delivered);

            var shipmentsInTransit = await _context.Shipments.CountAsync(s => s.Status == ShipmentStatus.InTransit);
            var shipmentsDelivered = await _context.Shipments.CountAsync(s => s.Status == ShipmentStatus.Delivered);

            var lowStockCount = await _context.Inventories.CountAsync(i => i.ReorderLevel > 0 && i.QuantityOnHand <= i.ReorderLevel);
            var damagedItemsCount = await _context.Inventories.CountAsync(i => i.DamagedQuantity > 0);

            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var revenueThisMonth = await _context.Orders
                .Where(o => o.Status == OrderStatus.Delivered && o.DeliveredAt != null && o.DeliveredAt >= startOfMonth)
                .SelectMany(o => o.Items)
                .SumAsync(i => (decimal?)(i.UnitPrice * i.Quantity)) ?? 0m;
            var revenueAllTime = await _context.Orders
                .Where(o => o.Status == OrderStatus.Delivered)
                .SelectMany(o => o.Items)
                .SumAsync(i => (decimal?)(i.UnitPrice * i.Quantity)) ?? 0m;

            var recentOrders = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Warehouse)
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .Take(8)
                .ToListAsync();

            var recentShipments = await _context.Shipments
                .Include(s => s.Order)
                .OrderByDescending(s => s.CreatedAt)
                .Take(8)
                .ToListAsync();

            var lowStockTop = await _context.Inventories
                .Include(i => i.Product)
                .Where(i => i.ReorderLevel > 0 && i.QuantityOnHand <= i.ReorderLevel)
                .OrderBy(i => i.QuantityOnHand)
                .Take(8)
                .ToListAsync();

            var topProducts = await _context.Products
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.Price)
                .Take(6)
                .ToListAsync();

            var model = new AdminDashboardViewModel
            {
                TotalUsers = totalUsers,
                TotalSuppliers = totalSuppliers,
                TotalWarehouses = totalWarehouses,
                TotalProducts = totalProducts,
                OrdersToday = ordersToday,
                OrdersPending = ordersPending,
                OrdersApproved = ordersApproved,
                OrdersProcessing = ordersProcessing,
                OrdersSentToSupplier = ordersSentToSupplier,
                OrdersShipped = ordersShipped,
                OrdersDelivered = ordersDelivered,
                ShipmentsInTransit = shipmentsInTransit,
                ShipmentsDelivered = shipmentsDelivered,
                LowStockCount = lowStockCount,
                DamagedItemsCount = damagedItemsCount,
                RevenueThisMonth = revenueThisMonth,
                RevenueAllTime = revenueAllTime,
                RecentOrders = recentOrders,
                RecentShipments = recentShipments,
                LowStockTop = lowStockTop,
                TopProducts = topProducts
            };

            return View("AdminDashboard", model);
        }

        if (User.IsInRole(UserRoles.Supplier))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.SupplierId == null)
            {
                return View();
            }

            var supplierId = user.SupplierId.Value;

            // Recent orders addressed to this supplier
            var recentOrders = await _context.Orders
                .Include(o => o.Warehouse)
                .Include(o => o.Items)
                .Where(o => o.SupplierId == supplierId)
                .OrderByDescending(o => o.CreatedAt)
                .Take(8)
                .ToListAsync();

            // Counts by status
            var ordersReceivedCount = await _context.Orders.CountAsync(o => o.SupplierId == supplierId && (o.Status == OrderStatus.SentToSupplier || o.Status == OrderStatus.PendingApproval));
            var ordersConfirmedCount = await _context.Orders.CountAsync(o => o.SupplierId == supplierId && o.Status == OrderStatus.ConfirmedBySupplier);
            var ordersProcessingCount = await _context.Orders.CountAsync(o => o.SupplierId == supplierId && o.Status == OrderStatus.Processing);
            var ordersShippedCount = await _context.Orders.CountAsync(o => o.SupplierId == supplierId && o.Status == OrderStatus.Shipped);
            var ordersDeliveredCount = await _context.Orders.CountAsync(o => o.SupplierId == supplierId && o.Status == OrderStatus.Delivered);

            // Recent shipments by this supplier
            var recentShipments = await _context.Shipments
                .Include(s => s.Order).ThenInclude(o => o.Warehouse)
                .Where(s => s.Order.SupplierId == supplierId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(8)
                .ToListAsync();

            // Products
            var activeProductsCount = await _context.Products.CountAsync(p => p.SupplierId == supplierId && p.IsActive);
            var featuredProducts = await _context.Products
                .Where(p => p.SupplierId == supplierId && p.IsActive)
                .OrderBy(p => p.Name)
                .Take(6)
                .ToListAsync();

            // Aggregates: total sales and revenue (Delivered orders)
            var deliveredOrdersForAgg = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.SupplierId == supplierId && o.Status == OrderStatus.Delivered)
                .ToListAsync();
            var totalSales = deliveredOrdersForAgg.Count;
            var totalRevenue = deliveredOrdersForAgg.Sum(o => o.TotalAmount);

            var model = new SupplierDashboardViewModel
            {
                SupplierName = (!string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : (user.UserName ?? "Supplier")),
                SupplierId = supplierId,
                RecentOrders = recentOrders,
                OrdersReceivedCount = ordersReceivedCount,
                OrdersConfirmedCount = ordersConfirmedCount,
                OrdersProcessingCount = ordersProcessingCount,
                OrdersShippedCount = ordersShippedCount,
                OrdersDeliveredCount = ordersDeliveredCount,
                RecentShipments = recentShipments,
                ActiveProductsCount = activeProductsCount,
                FeaturedProducts = featuredProducts,
                OutstandingInvoicesTotal = 0m
            };

            model.TotalSales = totalSales;
            model.TotalRevenue = totalRevenue;

            return View("SupplierDashboard", model);
        }

        if (User.IsInRole(UserRoles.WarehouseStaff))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.WarehouseId == null)
            {
                return View();
            }

            var warehouseId = user.WarehouseId.Value;

            // Inbound shipments in transit to this warehouse
            var inboundShipments = await _context.Shipments
                .Include(s => s.Order)
                .Where(s => s.Order.WarehouseId == warehouseId && s.Status == ShipmentStatus.InTransit)
                .OrderByDescending(s => s.CreatedAt)
                .Take(10)
                .ToListAsync();

            // Low stock items (QuantityOnHand <= ReorderLevel)
            var lowStock = await _context.Inventories
                .Include(i => i.Product)
                .Where(i => i.WarehouseId == warehouseId && i.ReorderLevel > 0 && i.QuantityOnHand <= i.ReorderLevel)
                .OrderBy(i => i.Product.Name)
                .Take(10)
                .ToListAsync();

            // Recent inventory transactions for this warehouse
            var recentTx = await _context.InventoryTransactions
                .Include(t => t.Product)
                .Where(t => t.WarehouseId == warehouseId)
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Take(10)
                .ToListAsync();

            // My open tasks (Putaway/Pick) for this user/warehouse
            var myTasks = await _context.WarehouseTasks
                .Include(t => t.Product)
                .Where(t => t.WarehouseId == warehouseId && (t.AssignedToUserId == user.Id || t.AssignedToUserId == null) && t.Status != WarehouseTaskStatus.Done)
                .OrderBy(t => t.Status).ThenBy(t => t.DueDate)
                .Take(8)
                .ToListAsync();

            var myOpenTasksCount = await _context.WarehouseTasks
                .CountAsync(t => t.WarehouseId == warehouseId && t.Status != WarehouseTaskStatus.Done);

            // Stats
            var today = DateTime.UtcNow.Date;
            var inboundTodayCount = await _context.Shipments
                .Include(s => s.Order)
                .CountAsync(s => s.Order.WarehouseId == warehouseId
                                  && s.Status == ShipmentStatus.InTransit
                                  && s.CreatedAt >= today);

            var lowStockCount = await _context.Inventories
                .CountAsync(i => i.WarehouseId == warehouseId && i.ReorderLevel > 0 && i.QuantityOnHand <= i.ReorderLevel);

            var outboundDueCount = await _context.Orders
                .CountAsync(o => o.WarehouseId == warehouseId
                                 && (o.Status == OrderStatus.Approved || o.Status == OrderStatus.Processing || o.Status == OrderStatus.ConfirmedBySupplier)
                                 && o.ShippedAt == null);

            // Expiry & Quarantine alerts and Outbound due lists
            var expiring = await _context.Inventories
                .Include(i => i.Product)
                .Where(i => i.WarehouseId == warehouseId && i.ExpiryDate != null && i.ExpiryDate >= today && i.ExpiryDate < today.AddDays(30))
                .OrderBy(i => i.ExpiryDate)
                .Take(10)
                .ToListAsync();

            var damaged = await _context.Inventories
                .Include(i => i.Product)
                .Where(i => i.WarehouseId == warehouseId && i.DamagedQuantity > 0)
                .OrderByDescending(i => i.DamagedQuantity)
                .Take(10)
                .ToListAsync();

            var outboundDue = await _context.Orders
                .Include(o => o.Items).ThenInclude(it => it.Product)
                .Where(o => o.WarehouseId == warehouseId && (o.Status == OrderStatus.Approved || o.Status == OrderStatus.Processing || o.Status == OrderStatus.ConfirmedBySupplier) && o.ShippedAt == null)
                .OrderBy(o => o.CreatedAt)
                .Take(10)
                .ToListAsync();

            var model = new WarehouseDashboardViewModel
            {
                WarehouseId = warehouseId,
                InboundShipments = inboundShipments,
                LowStockInventories = lowStock,
                RecentTransactions = recentTx,
                MyTasks = myTasks,
                InboundTodayCount = inboundTodayCount,
                LowStockCount = lowStockCount,
                OutboundDueCount = outboundDueCount,
                MyOpenTasksCount = myOpenTasksCount,
                ExpiringInventories = expiring,
                DamagedInventories = damaged,
                OutboundDueOrders = outboundDue
            };
            return View(model);
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
