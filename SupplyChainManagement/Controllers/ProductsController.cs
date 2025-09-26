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

namespace SupplyChainManagement.Controllers
{
    // Admin can manage all products; Supplier can manage only their own products
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Supplier + "," + UserRoles.WarehouseStaff)]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProductsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Products
        public async Task<IActionResult> Index()
        {
            IQueryable<Product> query = _context.Products.Include(p => p.Supplier).AsNoTracking();
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId != null)
                {
                    var supplierId = user.SupplierId.Value;
                    query = query.Where(p => p.SupplierId == supplierId);
                }
                else
                {
                    // Supplier without valid user should see nothing
                    query = query.Where(p => false);
                }
            }
            var products = await query.ToListAsync();
            return View(products);
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            Product product;
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId != null)
                {
                    var supplierId = user.SupplierId.Value;
                    product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplierId);
                }
                else
                {
                    return Forbid();
                }
            }
            else
            {
                product = await _context.Products
                    .Include(p => p.Supplier)
                    .FirstOrDefaultAsync(m => m.Id == id);
            }
            if (product == null) return NotFound();

            return View(product);
        }

        // GET: Products/Create
        public async Task<IActionResult> Create()
        {
            // Admin is read-only: cannot create products
            if (User.IsInRole(UserRoles.Admin))
            {
                return Forbid();
            }
            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,SKU,Price,SupplierId")] Product product)
        {
            // Admin is read-only: cannot create products
            if (User.IsInRole(UserRoles.Admin))
            {
                return Forbid();
            }
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId == null)
                {
                    ModelState.AddModelError(string.Empty, "Your account is not linked to a supplier.");
                    return View(product);
                }
                
                product.SupplierId = user.SupplierId.Value;
            }

            // Remove any validation errors for SupplierId since we set it automatically
            ModelState.Remove("SupplierId");
            ModelState.Remove("Supplier");

            if (ModelState.IsValid)
            {
                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            // Admin is read-only: cannot edit products
            if (User.IsInRole(UserRoles.Admin))
            {
                return Forbid();
            }
            if (id == null) return NotFound();

            Product existingProduct;
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId != null)
                {
                    var supplierId = user.SupplierId.Value;
                    existingProduct = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplierId);
                }
                else
                {
                    return Forbid();
                }
            }
            else
            {
                existingProduct = await _context.Products.FindAsync(id);
            }
            if (existingProduct == null) return NotFound();

            return View(existingProduct);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,SKU,Price,SupplierId")] Product product)
        {
            // Admin is read-only: cannot edit products
            if (User.IsInRole(UserRoles.Admin))
            {
                return Forbid();
            }
            if (id != product.Id) return NotFound();

            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId != null)
                {
                    var supplierId = user.SupplierId.Value;
                    var existingProduct = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplierId);
                    
                    if (existingProduct == null)
                    {
                        return NotFound();
                    }
                    
                    product.SupplierId = supplierId;
                }
                else
                {
                    return Forbid();
                }
            }

            // Remove any validation errors for SupplierId since we set it automatically
            ModelState.Remove("SupplierId");
            ModelState.Remove("Supplier");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Products.AnyAsync(e => e.Id == product.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            // Admin is read-only: cannot delete products
            if (User.IsInRole(UserRoles.Admin))
            {
                return Forbid();
            }
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null) return NotFound();
            if (User.IsInRole(UserRoles.Supplier))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SupplierId == null || product.SupplierId != user.SupplierId)
                {
                    return Forbid();
                }
            }

            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Admin is read-only: cannot delete products
            if (User.IsInRole(UserRoles.Admin))
            {
                return Forbid();
            }
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                if (User.IsInRole(UserRoles.Supplier))
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user?.SupplierId == null || product.SupplierId != user.SupplierId)
                    {
                        return Forbid();
                    }
                }
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Products/AddToCart/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> AddToCart(int id, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.WarehouseId.HasValue)
            {
                return Forbid("Your account is not linked to a warehouse.");
            }

            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            // Find or create a draft order for this warehouse and product's supplier
            var draft = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.WarehouseId == user.WarehouseId.Value
                                        && o.SupplierId == product.SupplierId
                                        && o.CreatedByUserId == user.Id
                                        && o.Status == OrderStatus.Draft);

            if (draft == null)
            {
                draft = new Order
                {
                    WarehouseId = user.WarehouseId.Value,
                    SupplierId = product.SupplierId,
                    CreatedByUserId = user.Id,
                    Status = OrderStatus.Draft,
                    CreatedAt = System.DateTime.UtcNow,
                    UpdatedAt = System.DateTime.UtcNow
                };
                _context.Orders.Add(draft);
                await _context.SaveChangesAsync();
                draft = await _context.Orders.Include(o => o.Items).FirstAsync(o => o.Id == draft.Id);
            }

            var existing = draft.Items.FirstOrDefault(i => i.ProductId == product.Id);

            // Enforce supplier stock availability (consider quantity already in draft)
            var alreadyInDraft = existing?.Quantity ?? 0;
            var remaining = product.StockQuantity - alreadyInDraft;
            if (remaining <= 0)
            {
                TempData["Error"] = $"Out of stock. Supplier has 0 available for '{product.Name}'.";
                return RedirectToAction(nameof(Details), new { id = product.Id });
            }
            if (quantity > remaining)
            {
                TempData["Error"] = $"Cannot add {quantity}. Only {remaining} available for '{product.Name}'.";
                return RedirectToAction(nameof(Details), new { id = product.Id });
            }

            if (existing != null)
            {
                existing.Quantity += quantity;
            }
            else
            {
                draft.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
            }

            draft.UpdatedAt = System.DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Added {quantity} x '{product.Name}' to your draft order.";
            return RedirectToAction("EditItems", "Orders", new { id = draft.Id });
        }
    }
}
