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
using ShipmentStatus = SupplyChainManagement.Models.Enums.ShipmentStatus;

namespace SupplyChainManagement.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrdersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // POST: Orders/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff + "," + UserRoles.Supplier)]
        public async Task<IActionResult> Cancel(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (User.IsInRole(UserRoles.Supplier))
            {
                // Supplier can cancel at any time but only their orders
                if (user.SupplierId == null || order.SupplierId != user.SupplierId)
                {
                    return Forbid();
                }
            }
            else if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                // Warehouse staff must belong to the same warehouse and can cancel only Draft/PendingApproval
                if (!user.WarehouseId.HasValue || order.WarehouseId != user.WarehouseId.Value)
                {
                    return Forbid();
                }
                if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.PendingApproval)
                {
                    TempData["Error"] = "Only draft or pending approval orders can be cancelled by warehouse staff.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }
            else
            {
                return Forbid();
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Order cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Orders/Pay/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Pay(int id, string method)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.WarehouseId.HasValue || order.WarehouseId != user.WarehouseId)
            {
                return Forbid();
            }

            // Disallow payment if terminal states
            if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Rejected || order.Status == OrderStatus.Delivered)
            {
                TempData["Error"] = "Cannot take payment for orders that are Cancelled, Rejected, or Delivered.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var payMethod = (method ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(payMethod)) payMethod = "Unknown";

            var total = order.Items?.Sum(i => i.UnitPrice * i.Quantity) ?? 0m;
            var stamp = DateTime.UtcNow.ToString("u");
            var line = $"[PAID {stamp}] Method={payMethod}; Amount={total:C}";
            order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? line : ($"{order.Notes}\n{line}");
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Payment recorded via {payMethod}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Orders
        public async Task<IActionResult> Index(string? view, string? status, string? q)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            IQueryable<Order> orders = _context.Orders
                .Include(o => o.CreatedByUser)
                .Include(o => o.Supplier)
                .Include(o => o.Warehouse)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product);

            // Filter orders based on user role
            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                if (!user.WarehouseId.HasValue)
                {
                    return Forbid("Your account is not linked to a warehouse.");
                }
                orders = orders.Where(o => o.WarehouseId == user.WarehouseId.Value);
            }
            else if (User.IsInRole(UserRoles.Supplier))
            {
                if (!user.SupplierId.HasValue)
                {
                    return Forbid("Your account is not linked to a supplier.");
                }
                orders = orders.Where(o => o.SupplierId == user.SupplierId.Value && o.Status != OrderStatus.Draft);

                // Supplier-specific views
                if (!string.IsNullOrEmpty(view))
                {
                    if (string.Equals(view, "received", StringComparison.OrdinalIgnoreCase))
                    {
                        orders = orders.Where(o => o.Status == OrderStatus.SentToSupplier);
                    }
                    else if (string.Equals(view, "confirmed", StringComparison.OrdinalIgnoreCase))
                    {
                        orders = orders.Where(o => o.Status == OrderStatus.ConfirmedBySupplier);
                    }
                }
                ViewBag.SupplierView = view;
            }

            // Apply status filter if provided (any role)
            if (!string.IsNullOrEmpty(status))
            {
                var st = status.Trim().ToLowerInvariant();
                orders = st switch
                {
                    "draft" => orders.Where(o => o.Status == OrderStatus.Draft),
                    "pending" or "pendingapproval" => orders.Where(o => o.Status == OrderStatus.PendingApproval),
                    "approved" => orders.Where(o => o.Status == OrderStatus.Approved),
                    "sent" or "senttosupplier" => orders.Where(o => o.Status == OrderStatus.SentToSupplier),
                    "confirmed" or "confirmedbysupplier" => orders.Where(o => o.Status == OrderStatus.ConfirmedBySupplier),
                    "processing" => orders.Where(o => o.Status == OrderStatus.Processing),
                    "shipped" => orders.Where(o => o.Status == OrderStatus.Shipped),
                    "delivered" => orders.Where(o => o.Status == OrderStatus.Delivered),
                    "rejected" => orders.Where(o => o.Status == OrderStatus.Rejected),
                    "cancelled" or "canceled" => orders.Where(o => o.Status == OrderStatus.Cancelled),
                    "due" => orders.Where(o => (o.Status == OrderStatus.Approved || o.Status == OrderStatus.Processing || o.Status == OrderStatus.ConfirmedBySupplier) && o.ShippedAt == null),
                    _ => orders
                };
                ViewBag.StatusFilter = status;
            }

            // Simple search by id, supplier, warehouse
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                orders = orders.Where(o => o.Id.ToString().Contains(term)
                                           || (o.Supplier != null && o.Supplier.Name.ToLower().Contains(term))
                                           || (o.Warehouse != null && o.Warehouse.Name.ToLower().Contains(term)));
                ViewBag.Query = q;
            }

            // Summary counts for header cards (scoped to current user's view)
            var scoped = orders.AsNoTracking();
            ViewBag.CountDraft = await scoped.CountAsync(o => o.Status == OrderStatus.Draft);
            ViewBag.CountPending = await scoped.CountAsync(o => o.Status == OrderStatus.PendingApproval);
            ViewBag.CountApproved = await scoped.CountAsync(o => o.Status == OrderStatus.Approved);
            ViewBag.CountProcessing = await scoped.CountAsync(o => o.Status == OrderStatus.Processing);
            ViewBag.CountShipped = await scoped.CountAsync(o => o.Status == OrderStatus.Shipped);
            ViewBag.CountDelivered = await scoped.CountAsync(o => o.Status == OrderStatus.Delivered);

            return View(await orders.OrderByDescending(o => o.CreatedAt).ToListAsync());
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.CreatedByUser)
                .Include(o => o.ApprovedByUser)
                .Include(o => o.Supplier)
                .Include(o => o.Warehouse)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            // Authorization
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (User.IsInRole(UserRoles.WarehouseStaff) && order.WarehouseId != user.WarehouseId)
            {
                return Forbid();
            }
            if (User.IsInRole(UserRoles.Supplier) && order.SupplierId != user.SupplierId)
            {
                return Forbid();
            }
            // Supplier cannot view Draft orders
            if (User.IsInRole(UserRoles.Supplier) && order.Status == OrderStatus.Draft)
            {
                return Forbid();
            }

            // Load shipments for this order so view can show shipment Details buttons
            var shipments = await _context.Shipments
                .Where(s => s.OrderId == order.Id)
                .OrderByDescending(s => s.Id)
                .ToListAsync();
            ViewData["Shipments"] = shipments;

            // Load payments and compute amount due
            var payments = await _context.Payments
                .Where(p => p.OrderId == order.Id)
                .OrderByDescending(p => p.Id)
                .ToListAsync();
            ViewData["Payments"] = payments;
            var paid = payments.Where(p => p.Status == PaymentStatus.Captured || p.Status == PaymentStatus.Authorized)
                               .Sum(p => p.Amount);
            ViewData["AmountPaid"] = paid;
            ViewData["AmountDue"] = Math.Max(0m, order.Items?.Sum(i => i.UnitPrice * i.Quantity) ?? 0m - paid);

            return View(order);
        }

        // GET: Orders/ProductsBySupplier?supplierId=123
        [HttpGet]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> ProductsBySupplier(int supplierId)
        {
            if (supplierId <= 0)
            {
                return Json(Array.Empty<object>());
            }
            var products = await _context.Products
                .Where(p => p.SupplierId == supplierId && p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new { id = p.Id, name = p.Name })
                .ToListAsync();
            return Json(products);
        }

        // GET: Orders/Create
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.WarehouseId.HasValue)
            {
                return Forbid("Your account is not linked to a warehouse.");
            }

            ViewData["SupplierId"] = new SelectList(await _context.Suppliers.ToListAsync(), "Id", "CompanyName");
            // Initialize with empty products until supplier is chosen
            ViewData["Products"] = Array.Empty<Product>();
            ViewBag.ProductId = new SelectList(Enumerable.Empty<Product>(), "Id", "Name");
            
            return View(new Order { 
                WarehouseId = user.WarehouseId.Value,
                CreatedByUserId = user.Id,
                Status = OrderStatus.Draft
            });
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Create([Bind("WarehouseId,SupplierId,Notes")] Order order, int? productId, int? quantity)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.WarehouseId.HasValue)
            {
                return Forbid("Your account is not linked to a warehouse.");
            }

            // Server-side set required fields irrespective of the posted form
            order.WarehouseId = user.WarehouseId.Value;
            order.CreatedByUserId = user.Id;

            // Enforce supplier selection
            if (order.SupplierId == 0)
            {
                ModelState.AddModelError("SupplierId", "The Supplier field is required.");
            }

            // Remove validation errors for navigation properties that are not bound by the form
            ModelState.Remove("CreatedByUser");
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("Warehouse");
            ModelState.Remove("Supplier");

            // If an initial product was selected, validate it belongs to the selected supplier
            if (productId.HasValue && productId.Value > 0)
            {
                if (order.SupplierId <= 0)
                {
                    ModelState.AddModelError("SupplierId", "Please select a supplier before choosing a product.");
                }
                else
                {
                    var prod = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId.Value);
                    if (prod == null)
                    {
                        ModelState.AddModelError("productId", "Selected product not found.");
                    }
                    else if (prod.SupplierId != order.SupplierId)
                    {
                        ModelState.AddModelError("productId", "Selected product does not belong to the chosen supplier.");
                    }
                    else if (!quantity.HasValue || quantity.Value <= 0)
                    {
                        ModelState.AddModelError("quantity", "Quantity must be greater than zero.");
                    }
                }
            }

            // Re-validate the model now that we set server-side values
            if (!ModelState.IsValid)
            {
                ViewData["SupplierId"] = new SelectList(await _context.Suppliers.ToListAsync(), "Id", "CompanyName", order.SupplierId);
                var filteredProducts = order.SupplierId > 0
                    ? await _context.Products.Where(p => p.SupplierId == order.SupplierId).ToListAsync()
                    : new List<Product>();
                ViewData["Products"] = filteredProducts;
                ViewBag.ProductId = new SelectList(filteredProducts, "Id", "Name");
                return View(order);
            }

            if (ModelState.IsValid)
            {
                order.CreatedByUserId = user.Id;
                order.Status = OrderStatus.Draft;
                order.CreatedAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;
                
                // If initial line was provided and valid, add it with stock validation
                if (productId.HasValue && productId.Value > 0 && quantity.HasValue && quantity.Value > 0)
                {
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId.Value && p.SupplierId == order.SupplierId);
                    if (product != null)
                    {
                        if (product.StockQuantity <= 0)
                        {
                            TempData["Error"] = $"Out of stock. Supplier has 0 available for '{product.Name}'.";
                            // Re-render the create view with selections
                            ViewData["SupplierId"] = new SelectList(await _context.Suppliers.ToListAsync(), "Id", "CompanyName", order.SupplierId);
                            var filteredProducts = await _context.Products.Where(p => p.SupplierId == order.SupplierId).ToListAsync();
                            ViewData["Products"] = filteredProducts;
                            ViewBag.ProductId = new SelectList(filteredProducts, "Id", "Name");
                            return View(order);
                        }
                        if (quantity.Value > product.StockQuantity)
                        {
                            TempData["Error"] = $"Cannot add {quantity.Value}. Only {product.StockQuantity} available for '{product.Name}'.";
                            ViewData["SupplierId"] = new SelectList(await _context.Suppliers.ToListAsync(), "Id", "CompanyName", order.SupplierId);
                            var filteredProducts = await _context.Products.Where(p => p.SupplierId == order.SupplierId).ToListAsync();
                            ViewData["Products"] = filteredProducts;
                            ViewBag.ProductId = new SelectList(filteredProducts, "Id", "Name");
                            return View(order);
                        }

                        order.Items = order.Items ?? new System.Collections.Generic.List<OrderItem>();
                        order.Items.Add(new OrderItem
                        {
                            ProductId = product.Id,
                            Quantity = quantity.Value,
                            UnitPrice = product.Price
                        });
                    }
                }

                _context.Add(order);
                await _context.SaveChangesAsync();
                
                return RedirectToAction(nameof(EditItems), new { id = order.Id });
            }

            ViewData["SupplierId"] = new SelectList(await _context.Suppliers.ToListAsync(), "Id", "CompanyName", order.SupplierId);
            ViewData["Products"] = await _context.Products.ToListAsync();
            ViewBag.ProductId = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
            
            return View(order);
        }

        // POST: Orders/AddItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> AddItem(int orderId, int productId, int quantity)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            // Authorization
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.WarehouseId.HasValue || order.WarehouseId != user.WarehouseId)
            {
                return Forbid();
            }

            if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.PendingApproval)
            {
                ModelState.AddModelError(string.Empty, "Can only add items to draft orders.");
                return RedirectToAction(nameof(EditItems), new { id = orderId });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            // Ensure product belongs to the order's supplier
            if (product.SupplierId != order.SupplierId)
            {
                TempData["Error"] = "Selected product does not belong to the chosen supplier.";
                return RedirectToAction(nameof(EditItems), new { id = orderId });
            }

            // Check if item already exists in order
            var existingItem = order.Items.FirstOrDefault(i => i.ProductId == productId);

            // Enforce supplier stock availability (consider quantity already in this order)
            var alreadyInOrder = existingItem?.Quantity ?? 0;
            var remaining = product.StockQuantity - alreadyInOrder;
            if (remaining <= 0)
            {
                TempData["Error"] = $"Out of stock. Supplier has 0 available for '{product.Name}'.";
                return RedirectToAction(nameof(EditItems), new { id = orderId });
            }
            if (quantity > remaining)
            {
                TempData["Error"] = $"Cannot add {quantity}. Only {remaining} available for '{product.Name}'.";
                return RedirectToAction(nameof(EditItems), new { id = orderId });
            }

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                order.Items.Add(new OrderItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
            }

            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(EditItems), new { id = orderId });
        }

        // POST: Orders/UpdateItemQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> UpdateItemQuantity(int orderId, int productId, int delta)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.WarehouseId.HasValue || order.WarehouseId != user.WarehouseId)
            {
                return Forbid();
            }

            if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.PendingApproval)
            {
                TempData["Error"] = "Can only modify items in draft or pending approval orders.";
                return RedirectToAction(nameof(EditItems), new { id = orderId });
            }

            var item = order.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null)
            {
                return RedirectToAction(nameof(EditItems), new { id = orderId });
            }

            var newQty = item.Quantity + delta;
            if (newQty <= 0)
            {
                _context.OrderItems.Remove(item);
            }
            else
            {
                // If increasing quantity, ensure sufficient supplier stock
                if (delta > 0)
                {
                    var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
                    if (product == null)
                    {
                        return RedirectToAction(nameof(EditItems), new { id = orderId });
                    }
                    var remaining = product.StockQuantity - item.Quantity;
                    if (delta > remaining)
                    {
                        TempData["Error"] = $"Cannot increase by {delta}. Only {remaining} additional units available for '{product.Name}'.";
                        return RedirectToAction(nameof(EditItems), new { id = orderId });
                    }
                }
                item.Quantity = newQty;
            }
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(EditItems), new { id = orderId });
        }

        // POST: Orders/RemoveItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> RemoveItem(int orderId, int productId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            // Authorization
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.WarehouseId.HasValue || order.WarehouseId != user.WarehouseId)
            {
                return Forbid();
            }

            if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.PendingApproval)
            {
                ModelState.AddModelError(string.Empty, "Can only modify items in draft orders.");
                return RedirectToAction(nameof(EditItems), new { id = orderId });
            }

            var item = order.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                _context.OrderItems.Remove(item);
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(EditItems), new { id = orderId });
        }

        // GET: Orders/EditItems/5
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> EditItems(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Supplier)
                .Include(o => o.Warehouse)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            // Authorization
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.WarehouseId.HasValue || order.WarehouseId != user.WarehouseId)
            {
                return Forbid();
            }

            if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.PendingApproval)
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            var filtered = await _context.Products.Where(p => p.SupplierId == order.SupplierId).ToListAsync();
            ViewData["Products"] = filtered;
            ViewBag.ProductId = new SelectList(filtered, "Id", "Name");
            return View(order);
        }

        // POST: Orders/Submit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Submit(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Authorization
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.WarehouseId.HasValue || order.WarehouseId != user.WarehouseId)
            {
                return Forbid();
            }

            if (order.Status != OrderStatus.Draft)
            {
                ModelState.AddModelError(string.Empty, "Only draft orders can be submitted.");
                return RedirectToAction(nameof(Index));
            }

            if (!order.Items.Any())
            {
                ModelState.AddModelError(string.Empty, "Cannot submit an empty order.");
                return RedirectToAction(nameof(EditItems), new { id });
            }

            order.Status = OrderStatus.PendingApproval;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // TODO: Send notification to admin for approval

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Orders/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> Approve(int id, string? notes = null)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status != OrderStatus.PendingApproval)
            {
                ModelState.AddModelError(string.Empty, "Only orders pending approval can be approved.");
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            order.Status = OrderStatus.Approved;
            order.ApprovedAt = DateTime.UtcNow;
            order.ApprovedByUserId = user.Id;
            order.Notes = notes;
            order.UpdatedAt = DateTime.UtcNow;

            // TODO: Send notification to warehouse staff

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Orders/SendToSupplier/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> SendToSupplier(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Supplier)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            if (order.Status != OrderStatus.Approved)
            {
                TempData["Error"] = "Only approved orders can be sent to supplier.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = OrderStatus.SentToSupplier;
            order.UpdatedAt = DateTime.UtcNow;

            // TODO: Notify supplier via email

            await _context.SaveChangesAsync();
            TempData["Success"] = "Order sent to supplier.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Orders/SupplierConfirm/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> SupplierConfirm(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user?.SupplierId == null || order.SupplierId != user.SupplierId)
            {
                return Forbid();
            }

            if (order.Status != OrderStatus.SentToSupplier)
            {
                TempData["Error"] = "Only orders sent to supplier can be confirmed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Decrease supplier product stock for each item
            using (var tx = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId && p.SupplierId == order.SupplierId);
                        if (product == null)
                        {
                            await tx.RollbackAsync();
                            TempData["Error"] = "Product not found for this supplier.";
                            return RedirectToAction(nameof(Details), new { id });
                        }
                        if (product.StockQuantity < item.Quantity)
                        {
                            await tx.RollbackAsync();
                            TempData["Error"] = $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, needed: {item.Quantity}.";
                            return RedirectToAction(nameof(Details), new { id });
                        }
                        product.StockQuantity -= item.Quantity;
                    }

                    order.Status = OrderStatus.ConfirmedBySupplier;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    TempData["Success"] = "Order confirmed by supplier and product stock updated.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (Exception)
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
        }

        // POST: Orders/SupplierStartProcessing/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Supplier)]
        public async Task<IActionResult> SupplierStartProcessing(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Supplier)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user?.SupplierId == null || order.SupplierId != user.SupplierId)
            {
                return Forbid();
            }

            if (order.Status != OrderStatus.ConfirmedBySupplier)
            {
                TempData["Error"] = "Only confirmed orders can be marked as processing by supplier.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = OrderStatus.Processing;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Order status updated to Processing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Orders/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> Reject(int id, string? reason = null)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status != OrderStatus.PendingApproval)
            {
                ModelState.AddModelError(string.Empty, "Only orders pending approval can be rejected.");
                return RedirectToAction(nameof(Index));
            }

            order.Status = OrderStatus.Rejected;
            order.Notes = reason;
            order.UpdatedAt = DateTime.UtcNow;

            // TODO: Send notification to warehouse staff

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Orders/Process/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Process(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Authorization
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Only WarehouseStaff can mark Processing, ensure same warehouse
            if (!user.WarehouseId.HasValue || order.WarehouseId != user.WarehouseId)
            {
                return Forbid();
            }

            if (order.Status != OrderStatus.Approved)
            {
                ModelState.AddModelError(string.Empty, "Only approved orders can be processed.");
                return RedirectToAction(nameof(Index));
            }

            order.Status = OrderStatus.Processing;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Orders/Ship/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> Ship(int id, string? trackingNumber = null)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Warehouse)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Authorization
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (User.IsInRole(UserRoles.WarehouseStaff) && 
                (!user.WarehouseId.HasValue || order.WarehouseId != user.WarehouseId))
            {
                return Forbid();
            }

            if (order.Status != OrderStatus.Processing)
            {
                ModelState.AddModelError(string.Empty, "Only orders in processing can be marked as shipped.");
                return RedirectToAction(nameof(Index));
            }

            order.Status = OrderStatus.Shipped;
            order.ShippedAt = DateTime.UtcNow;
            order.TrackingNumber = trackingNumber;
            order.UpdatedAt = DateTime.UtcNow;

            // Update inventory levels
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    foreach (var item in order.Items)
                    {
                        var inventory = await _context.Inventories
                            .Include(i => i.Product)
                            .FirstOrDefaultAsync(i => i.WarehouseId == order.WarehouseId && i.ProductId == item.ProductId);

                        if (inventory == null || inventory.QuantityOnHand < item.Quantity)
                        {
                            await transaction.RollbackAsync();
                            TempData["Error"] = $"Insufficient inventory for product '{inventory?.Product?.Name ?? "Unknown"}'.";
                            return RedirectToAction(nameof(Details), new { id });
                        }

                        inventory.QuantityOnHand -= item.Quantity;
                    }

                    // Create shipment record
                    var shipment = new Shipment
                    {
                        OrderId = order.Id,
                        Status = ShipmentStatus.InTransit,
                        ShippedAt = DateTime.UtcNow,
                        TrackingNumber = trackingNumber
                    };
                    
                    _context.Shipments.Add(shipment);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    
                    TempData["Success"] = "Order shipped and inventory updated successfully.";
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Orders/Delete/5
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var order = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Supplier)
                .Include(o => o.Warehouse)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();
            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            // Remove related shipments if any
            var shipments = await _context.Shipments.Where(s => s.OrderId == id).ToListAsync();
            if (shipments.Any())
            {
                _context.Shipments.RemoveRange(shipments);
            }

            // Remove items then order
            if (order.Items?.Any() == true)
            {
                _context.OrderItems.RemoveRange(order.Items);
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Order deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
