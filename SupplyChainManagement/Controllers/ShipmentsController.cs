using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupplyChainManagement.Data;
using SupplyChainManagement.Models;
using SupplyChainManagement.Models.Enums;
using ShipmentStatus = SupplyChainManagement.Models.Enums.ShipmentStatus;
using OrderStatus = SupplyChainManagement.Models.Enums.OrderStatus;
using Microsoft.AspNetCore.Identity;

namespace SupplyChainManagement.Controllers
{
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Supplier + "," + UserRoles.WarehouseStaff)]
    public class ShipmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShipmentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Shipments
        public async Task<IActionResult> Index()
        {
            var shipments = _context.Shipments
                .Include(s => s.Order)
                .AsNoTracking();

            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId == null) return Forbid("Your account is not linked to a supplier.");
                var supplierId = user.SupplierId.Value;
                shipments = shipments.Where(s => s.Order.SupplierId == supplierId);
            }
            else if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.WarehouseId == null) return Forbid("Your account is not linked to a warehouse.");
                var warehouseId = user.WarehouseId.Value;
                shipments = shipments.Where(s => s.Order.WarehouseId == warehouseId);
            }

            var list = await shipments.OrderByDescending(s => s.Id).ToListAsync();
            return View(list);
        }

        // GET: Shipments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var shipment = await _context.Shipments
                .Include(s => s.Order)
                    .ThenInclude(o => o.Items)
                        .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (shipment == null) return NotFound();
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId == null || shipment.Order.SupplierId != user.SupplierId) return Forbid();
            }
            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.WarehouseId == null || shipment.Order.WarehouseId != user.WarehouseId) return Forbid();
            }
            return View(shipment);
        }

        // GET: Shipments/Create
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> Create(int? orderId)
        {
            IQueryable<Order> allowed = _context.Orders.AsNoTracking();
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId == null) return Forbid("Your account is not linked to a supplier.");
                var supplierId = user.SupplierId.Value;
                allowed = allowed.Where(o => o.SupplierId == supplierId 
                    && (o.Status == OrderStatus.Approved || o.Status == OrderStatus.Processing || o.Status == OrderStatus.ConfirmedBySupplier));
            }
            ViewData["OrderId"] = new SelectList(await allowed.OrderByDescending(o => o.Id).ToListAsync(), "Id", "Id", orderId);
            return View(new Shipment { Status = ShipmentStatus.InTransit });
        }

        // POST: Shipments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> Create([Bind("OrderId,Courier,TrackingNumber,Status,ShippedAt,DeliveredAt")] Shipment shipment)
        {
            if (ModelState.IsValid)
            {
                // Authorization: supplier can only create shipment for own order
                if (User.IsInRole(UserRoles.Supplier))
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user?.SupplierId == null) return Forbid();
                    var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == shipment.OrderId);
                    if (order == null) return NotFound();
                    if (order.SupplierId != user.SupplierId) return Forbid();
                    // optionally ensure suitable status
                    if (order.Status != OrderStatus.Approved && order.Status != OrderStatus.Processing && order.Status != OrderStatus.ConfirmedBySupplier)
                    {
                        ModelState.AddModelError(string.Empty, "Shipment can be created only for approved or processing orders.");
                        ViewData["OrderId"] = new SelectList(_context.Orders.Where(o => o.SupplierId == user.SupplierId), "Id", "Id", shipment.OrderId);
                        return View(shipment);
                    }
                }
                if (shipment.Status == ShipmentStatus.InTransit && shipment.ShippedAt == null)
                {
                    shipment.ShippedAt = DateTime.UtcNow;
                }
                _context.Add(shipment);
                // If shipment is shipped (InTransit), mark order as Shipped
                var relatedOrder = await _context.Orders.FirstOrDefaultAsync(o => o.Id == shipment.OrderId);
                if (relatedOrder != null && relatedOrder.Status != OrderStatus.Delivered)
                {
                    if (shipment.Status == ShipmentStatus.InTransit)
                    {
                        relatedOrder.Status = OrderStatus.Shipped;
                        if (relatedOrder.ShippedAt == null) relatedOrder.ShippedAt = shipment.ShippedAt ?? DateTime.UtcNow;
                        relatedOrder.UpdatedAt = DateTime.UtcNow;
                    }
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = "Shipment created.";
                return RedirectToAction(nameof(Index));
            }
            // Rebuild allowed orders list on validation error
            IQueryable<Order> allowed = _context.Orders.AsNoTracking();
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId != null)
                {
                    var supplierId = user.SupplierId.Value;
                    allowed = allowed.Where(o => o.SupplierId == supplierId);
                }
            }
            ViewData["OrderId"] = new SelectList(await allowed.ToListAsync(), "Id", "Id", shipment.OrderId);
            return View(shipment);
        }

        // GET: Shipments/Edit/5
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null) return NotFound();
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == shipment.OrderId);
                if (user?.SupplierId == null || order == null || order.SupplierId != user.SupplierId) return Forbid();
            }
            ViewData["OrderId"] = new SelectList(_context.Orders, "Id", "Id", shipment.OrderId);
            return View(shipment);
        }

        // POST: Shipments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,OrderId,Courier,TrackingNumber,Status,ShippedAt,DeliveredAt")] Shipment shipment)
        {
            if (id != shipment.Id) return NotFound();
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == shipment.OrderId);
                if (user?.SupplierId == null || order == null || order.SupplierId != user.SupplierId) return Forbid();
            }
            if (ModelState.IsValid)
            {
                try
                {
                    // auto-manage timestamps
                    if (shipment.Status == ShipmentStatus.InTransit && shipment.ShippedAt == null)
                        shipment.ShippedAt = DateTime.UtcNow;
                    if (shipment.Status == ShipmentStatus.Delivered && shipment.DeliveredAt == null)
                    {
                        // Suppliers are not allowed to mark as Delivered via edit
                        if (User.IsInRole(UserRoles.Supplier))
                        {
                            ModelState.AddModelError(string.Empty, "Only warehouse staff can mark shipments as Delivered.");
                            ViewData["OrderId"] = new SelectList(_context.Orders, "Id", "Id", shipment.OrderId);
                            return View(shipment);
                        }
                        shipment.DeliveredAt = DateTime.UtcNow;
                    }

                    _context.Update(shipment);

                    // Reflect shipment status to order
                    var ord = await _context.Orders.FirstOrDefaultAsync(o => o.Id == shipment.OrderId);
                    if (ord != null)
                    {
                        if (shipment.Status == ShipmentStatus.InTransit && ord.Status != OrderStatus.Delivered)
                        {
                            ord.Status = OrderStatus.Shipped;
                            if (ord.ShippedAt == null) ord.ShippedAt = shipment.ShippedAt ?? DateTime.UtcNow;
                            ord.UpdatedAt = DateTime.UtcNow;
                        }
                        else if (shipment.Status == ShipmentStatus.Delivered)
                        {
                            ord.Status = OrderStatus.Delivered;
                            ord.DeliveredAt = shipment.DeliveredAt ?? DateTime.UtcNow;
                            ord.UpdatedAt = DateTime.UtcNow;

                            // Auto-create Invoice for delivered order (idempotent)
                            var hasInvoice = await _context.Invoices.AnyAsync(iv => iv.OrderId == ord.Id);
                            if (!hasInvoice)
                            {
                                var amount = await _context.OrderItems
                                    .Where(oi => oi.OrderId == ord.Id)
                                    .SumAsync(oi => (decimal?)(oi.UnitPrice * oi.Quantity)) ?? 0m;
                                _context.Invoices.Add(new Invoice
                                {
                                    OrderId = ord.Id,
                                    SupplierId = ord.SupplierId,
                                    Amount = amount,
                                    Status = InvoiceStatus.Unpaid,
                                    IssuedAt = DateTime.UtcNow,
                                    DueDate = DateTime.UtcNow.AddDays(30)
                                });
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Shipment updated.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Shipments.AnyAsync(e => e.Id == shipment.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["OrderId"] = new SelectList(_context.Orders, "Id", "Id", shipment.OrderId);
            return View(shipment);
        }

        // GET: Shipments/Delete/5
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var shipment = await _context.Shipments
                .Include(s => s.Order)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (shipment == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user?.SupplierId == null || shipment.Order.SupplierId != user.SupplierId) return Forbid();
            return View(shipment);
        }

        // POST: Shipments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment != null)
            {
                var user = await _userManager.GetUserAsync(User);
                var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == shipment.OrderId);
                if (user?.SupplierId == null || order == null || order.SupplierId != user.SupplierId) return Forbid();
                _context.Shipments.Remove(shipment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Shipment deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Shipments/MarkDelivered
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> MarkDelivered(int id)
        {
            var shipment = await _context.Shipments
                .Include(s => s.Order)
                    .ThenInclude(o => o.Items)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (shipment == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var orderAuth = shipment.Order;
                if (user?.WarehouseId == null || orderAuth == null || orderAuth.WarehouseId != user.WarehouseId) return Forbid();
            }

            // Idempotency: if already delivered, do not re-update inventory
            if (shipment.Status == ShipmentStatus.Delivered)
            {
                TempData["Success"] = "Shipment already marked as delivered.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Update inventory for the order's warehouse: add received quantities
            using (var tx = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var order = shipment.Order;
                    var warehouseId = order.WarehouseId;
                    foreach (var item in order.Items)
                    {
                        var inv = await _context.Inventories
                            .FirstOrDefaultAsync(i => i.WarehouseId == warehouseId && i.ProductId == item.ProductId);
                        if (inv == null)
                        {
                            inv = new Inventory
                            {
                                WarehouseId = warehouseId,
                                ProductId = item.ProductId,
                                QuantityOnHand = item.Quantity,
                                ReorderLevel = 0
                            };
                            _context.Inventories.Add(inv);
                        }
                        else
                        {
                            inv.QuantityOnHand += item.Quantity;
                        }

                        // Log inventory receipt transaction for traceability
                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            WarehouseId = warehouseId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity, // positive for receipt
                            Type = InventoryTransactionType.Receipt,
                            ReferenceType = "Shipment",
                            ReferenceId = shipment.Id,
                            PerformedByUserId = user?.Id,
                            CreatedAt = DateTime.UtcNow,
                            Notes = $"Shipment #{shipment.Id} delivered for Order #{order.Id}"
                        });

                        // Create Putaway task for each item
                        _context.WarehouseTasks.Add(new WarehouseTask
                        {
                            WarehouseId = warehouseId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            Bin = inv?.Bin,
                            Type = WarehouseTaskType.Putaway,
                            Status = WarehouseTaskStatus.Open,
                            DueDate = DateTime.UtcNow.AddDays(1)
                        });
                    }

                    shipment.Status = ShipmentStatus.Delivered;
                    shipment.DeliveredAt = DateTime.UtcNow;
                    // Also mark the related order as Delivered
                    var orderToUpdate = order; // shipment.Order
                    if (orderToUpdate != null)
                    {
                        orderToUpdate.Status = OrderStatus.Delivered;
                        orderToUpdate.DeliveredAt = DateTime.UtcNow;
                        orderToUpdate.UpdatedAt = DateTime.UtcNow;

                        // Auto-create Invoice for delivered order (idempotent)
                        var hasInvoice = await _context.Invoices.AnyAsync(iv => iv.OrderId == orderToUpdate.Id);
                        if (!hasInvoice)
                        {
                            var amount = orderToUpdate.Items?.Sum(it => it.UnitPrice * it.Quantity) ?? 0m;
                            _context.Invoices.Add(new Invoice
                            {
                                OrderId = orderToUpdate.Id,
                                SupplierId = orderToUpdate.SupplierId,
                                Amount = amount,
                                Status = InvoiceStatus.Unpaid,
                                IssuedAt = DateTime.UtcNow,
                                DueDate = DateTime.UtcNow.AddDays(30)
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    TempData["Success"] = "Shipment marked as delivered and inventory updated.";
                }
                catch (Exception)
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Shipments/MarkShipped
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> MarkShipped(int id)
        {
            var shipment = await _context.Shipments.FirstOrDefaultAsync(s => s.Id == id);
            if (shipment == null) return NotFound();

            // Authorization: supplier can only update own shipments
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == shipment.OrderId);
            if (user?.SupplierId == null || order == null || order.SupplierId != user.SupplierId) return Forbid();

            if (shipment.Status == ShipmentStatus.InTransit)
            {
                TempData["Success"] = "Shipment already marked as shipped.";
                return RedirectToAction(nameof(Details), new { id });
            }

            shipment.Status = ShipmentStatus.InTransit;
            if (shipment.ShippedAt == null) shipment.ShippedAt = DateTime.UtcNow;

            // Reflect to order if not delivered yet
            if (order.Status != OrderStatus.Delivered)
            {
                order.Status = OrderStatus.Shipped;
                if (order.ShippedAt == null) order.ShippedAt = shipment.ShippedAt;
                order.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Shipment marked as shipped.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Shipments/MarkDelayed
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> MarkDelayed(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null) return NotFound();
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == shipment.OrderId);
                if (user?.SupplierId == null || order == null || order.SupplierId != user.SupplierId) return Forbid();
            }
            shipment.Status = ShipmentStatus.Delayed;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Shipment marked as delayed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Shipments/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null) return NotFound();
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == shipment.OrderId);
                if (user?.SupplierId == null || order == null || order.SupplierId != user.SupplierId) return Forbid();
            }
            shipment.Status = ShipmentStatus.Cancelled;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Shipment cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
