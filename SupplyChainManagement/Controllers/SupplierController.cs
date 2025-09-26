using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupplyChainManagement.Data;
using SupplyChainManagement.Models;
using SupplyChainManagement.Models.Enums;
using OrderStatus = SupplyChainManagement.Models.Enums.OrderStatus;

namespace SupplyChainManagement.Controllers
{
    [Authorize(Roles = UserRoles.Supplier)]
    public class SupplierController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SupplierController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Orders Received - See purchase orders from Admin
        public async Task<IActionResult> OrdersReceived()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            
            // Debug: Check user details
            ViewBag.DebugInfo = $"User: {currentUser?.Email}, SupplierId: {currentUser?.SupplierId}";
            
            if (currentUser?.SupplierId == null)
            {
                // Try to find and associate supplier if user exists but not linked
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Email == currentUser.Email);
                if (supplier != null)
                {
                    currentUser.SupplierId = supplier.Id;
                    await _userManager.UpdateAsync(currentUser);
                }
                else
                {
                    TempData["Error"] = $"You are not associated with any supplier. User: {currentUser?.Email}, SupplierId: {currentUser?.SupplierId}";
                    return RedirectToAction("Index", "Home");
                }
            }

            var orders = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.CreatedByUser)
                .Include(o => o.Warehouse)
                .Where(o => o.SupplierId == currentUser.SupplierId && o.Status == OrderStatus.SentToSupplier)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // Confirm Orders - Accept/reject orders
        public async Task<IActionResult> ConfirmOrders()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SupplierId == null)
            {
                TempData["Error"] = "You are not associated with any supplier.";
                return RedirectToAction("Index", "Home");
            }

            var pendingOrders = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.CreatedByUser)
                .Include(o => o.Warehouse)
                .Where(o => o.SupplierId == currentUser.SupplierId && 
                           o.Status == OrderStatus.ConfirmedBySupplier)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(pendingOrders);
        }

        [HttpPost]
        public async Task<IActionResult> AcceptOrder(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.SupplierId == currentUser.SupplierId);

            if (order == null)
            {
                TempData["Error"] = "Order not found or you don't have permission to modify it.";
                return RedirectToAction(nameof(ConfirmOrders));
            }

            order.Status = OrderStatus.Approved;
            order.ApprovedAt = DateTime.UtcNow;
            order.ApprovedByUserId = currentUser.Id;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Order accepted successfully.";
            return RedirectToAction(nameof(ConfirmOrders));
        }

        [HttpPost]
        public async Task<IActionResult> RejectOrder(int id, string reason)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.SupplierId == currentUser.SupplierId);

            if (order == null)
            {
                TempData["Error"] = "Order not found or you don't have permission to modify it.";
                return RedirectToAction(nameof(ConfirmOrders));
            }

            order.Status = OrderStatus.Rejected;
            order.Notes = reason;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Order rejected successfully.";
            return RedirectToAction(nameof(ConfirmOrders));
        }

        // Product Catalog - Update product availability, pricing, lead times
        public async Task<IActionResult> ProductCatalog()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SupplierId == null)
            {
                // Try to find and associate supplier if user exists but not linked
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Email == currentUser.Email);
                if (supplier != null)
                {
                    currentUser.SupplierId = supplier.Id;
                    await _userManager.UpdateAsync(currentUser);
                }
                else
                {
                    TempData["Error"] = "You are not associated with any supplier.";
                    return RedirectToAction("Index", "Home");
                }
            }

            var products = await _context.Products
                .Where(p => p.SupplierId == currentUser.SupplierId)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View(products);
        }

        public async Task<IActionResult> CreateProduct()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SupplierId == null)
            {
                TempData["Error"] = "You are not associated with any supplier.";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SupplierId == null)
            {
                // Try to find and associate supplier if user exists but not linked
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Email == currentUser.Email);
                if (supplier != null)
                {
                    currentUser.SupplierId = supplier.Id;
                    await _userManager.UpdateAsync(currentUser);
                }
                else
                {
                    TempData["Error"] = "You are not associated with any supplier.";
                    return RedirectToAction("Index", "Home");
                }
            }

            // Automatically set the supplier ID BEFORE validation
            product.SupplierId = currentUser.SupplierId.Value;
            
            // Generate SKU if not provided
            if (string.IsNullOrEmpty(product.SKU))
            {
                product.SKU = $"SKU-{DateTime.Now:yyyyMMddHHmmss}";
            }

            // Remove any validation errors for SupplierId since we set it automatically
            ModelState.Remove("SupplierId");
            ModelState.Remove("Supplier");

            if (ModelState.IsValid)
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Product created successfully.";
                return RedirectToAction(nameof(ProductCatalog));
            }

            return View(product);
        }

        public async Task<IActionResult> EditProduct(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == currentUser.SupplierId);

            if (product == null)
            {
                TempData["Error"] = "Product not found or you don't have permission to edit it.";
                return RedirectToAction(nameof(ProductCatalog));
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, Product product)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == currentUser.SupplierId);

            if (existingProduct == null)
            {
                TempData["Error"] = "Product not found or you don't have permission to edit it.";
                return RedirectToAction(nameof(ProductCatalog));
            }

            // Remove any validation errors for SupplierId since we don't allow changing it
            ModelState.Remove("SupplierId");
            ModelState.Remove("Supplier");

            if (ModelState.IsValid)
            {
                existingProduct.Name = product.Name;
                existingProduct.Description = product.Description;
                existingProduct.Price = product.Price;
                existingProduct.StockQuantity = product.StockQuantity;
                existingProduct.SKU = product.SKU;
                existingProduct.UnitOfMeasure = product.UnitOfMeasure;
                existingProduct.IsActive = product.IsActive;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Product updated successfully.";
                return RedirectToAction(nameof(ProductCatalog));
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SupplierId == null)
            {
                TempData["Error"] = "You are not associated with any supplier.";
                return RedirectToAction("Index", "Home");
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == currentUser.SupplierId);

            if (product == null)
            {
                TempData["Error"] = "Product not found or you don't have permission to delete it.";
                return RedirectToAction(nameof(ProductCatalog));
            }

            // Check if product is used in any orders
            var hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
            if (hasOrders)
            {
                TempData["Error"] = "Cannot delete product as it is referenced in existing orders.";
                return RedirectToAction(nameof(ProductCatalog));
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = $"Product '{product.Name}' has been deleted successfully.";
            return RedirectToAction(nameof(ProductCatalog));
        }

        // Shipment Updates - Enter tracking details, expected delivery dates
        public async Task<IActionResult> ShipmentUpdates()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SupplierId == null)
            {
                TempData["Error"] = "You are not associated with any supplier.";
                return RedirectToAction("Index", "Home");
            }

            var shipments = await _context.Shipments
                .Include(s => s.Order)
                .Where(s => s.Order.SupplierId == currentUser.SupplierId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(shipments);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateShipment(int id, string trackingNumber, DateTime? expectedDelivery)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var shipment = await _context.Shipments
                .Include(s => s.Order)
                .FirstOrDefaultAsync(s => s.Id == id && s.Order.SupplierId == currentUser.SupplierId);

            if (shipment == null)
            {
                TempData["Error"] = "Shipment not found or you don't have permission to update it.";
                return RedirectToAction(nameof(ShipmentUpdates));
            }

            if (!string.IsNullOrEmpty(trackingNumber))
            {
                shipment.TrackingNumber = trackingNumber;
            }

            if (expectedDelivery.HasValue)
            {
                shipment.DeliveredAt = expectedDelivery;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Shipment updated successfully.";
            return RedirectToAction(nameof(ShipmentUpdates));
        }

        // Invoices & Payments - Create invoices and track payment status
        public async Task<IActionResult> InvoicesPayments()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SupplierId == null)
            {
                TempData["Error"] = "You are not associated with any supplier.";
                return RedirectToAction("Index", "Home");
            }

            var supplierId = currentUser.SupplierId.Value;

            var invoices = await _context.Invoices
                .Include(i => i.Order).ThenInclude(o => o.Items)
                .Where(i => i.SupplierId == supplierId)
                .OrderByDescending(i => i.IssuedAt)
                .ToListAsync();

            // Eligible orders for invoicing: Delivered (or Shipped if desired) and no existing invoice
            var eligibleOrders = await _context.Orders
                .Include(o => o.Items).ThenInclude(it => it.Product)
                .Where(o => o.SupplierId == supplierId
                            && (o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Shipped)
                            && !_context.Invoices.Any(inv => inv.OrderId == o.Id))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var vm = new SupplyChainManagement.Models.ViewModels.SupplierInvoicesViewModel
            {
                SupplierId = supplierId,
                SupplierName = currentUser.FullName ?? currentUser.UserName ?? "Supplier",
                Invoices = invoices,
                EligibleOrders = eligibleOrders,
                TotalInvoiced = invoices.Sum(i => i.Amount),
                TotalPaid = invoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Amount),
                TotalUnpaid = invoices.Where(i => i.Status != InvoiceStatus.Paid).Sum(i => i.Amount)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvoice(int orderId, DateTime? dueDate)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SupplierId == null)
            {
                TempData["Error"] = "You are not associated with any supplier.";
                return RedirectToAction(nameof(InvoicesPayments));
            }

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.SupplierId == currentUser.SupplierId);
            if (order == null)
            {
                TempData["Error"] = "Order not found or not eligible.";
                return RedirectToAction(nameof(InvoicesPayments));
            }
            if (_context.Invoices.Any(i => i.OrderId == order.Id))
            {
                TempData["Error"] = "Invoice already exists for this order.";
                return RedirectToAction(nameof(InvoicesPayments));
            }

            var invoice = new Invoice
            {
                OrderId = order.Id,
                SupplierId = currentUser.SupplierId.Value,
                Amount = order.TotalAmount,
                DueDate = dueDate,
                Status = InvoiceStatus.Unpaid,
                IssuedAt = DateTime.UtcNow
            };
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Invoice #{invoice.Id} created for Order #{order.Id}.";
            return RedirectToAction(nameof(InvoicesPayments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkInvoicePaid(int id, string? method)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var invoice = await _context.Invoices
                .Include(i => i.Order)
                .FirstOrDefaultAsync(i => i.Id == id && i.SupplierId == currentUser.SupplierId);
            if (invoice == null)
            {
                TempData["Error"] = "Invoice not found.";
                return RedirectToAction(nameof(InvoicesPayments));
            }

            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAt = DateTime.UtcNow;
            invoice.PaymentMethod = method;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Invoice marked as paid.";
            return RedirectToAction(nameof(InvoicesPayments));
        }
    }
}
