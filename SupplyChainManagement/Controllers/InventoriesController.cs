using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupplyChainManagement.Data;
using SupplyChainManagement.Models;
using SupplyChainManagement.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace SupplyChainManagement.Controllers
{
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseStaff)]
    public class InventoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InventoriesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Inventories
        public async Task<IActionResult> Index()
        {
            var query = _context.Inventories
                .Include(i => i.Product).ThenInclude(p => p.Supplier)
                .Include(i => i.Warehouse)
                .AsNoTracking();

            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.WarehouseId == null) return Forbid("Your account is not linked to a warehouse.");
                query = query.Where(i => i.WarehouseId == user.WarehouseId.Value);
            }

            var list = await query
                .OrderBy(i => i.Product.Name)
                .ThenBy(i => i.Aisle)
                .ThenBy(i => i.Shelf)
                .ThenBy(i => i.Bin)
                .ToListAsync();
            return View(list);
        }

        // GET: Inventories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var inv = await _context.Inventories
                .Include(i => i.Product).ThenInclude(p => p.Supplier)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (inv == null) return NotFound();
            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.WarehouseId == null || inv.WarehouseId != user.WarehouseId) return Forbid();
            }
            return View(inv);
        }

        // GET: Inventories/Create
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Create()
        {
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name");
            // For WarehouseStaff, prefill WarehouseId and show as read-only
            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.WarehouseId == null) return Forbid("Your account is not linked to a warehouse.");
                var model = new Inventory { WarehouseId = user.WarehouseId.Value };
                return View(model);
            }
            return View();
        }

        // POST: Inventories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Create([Bind("ProductId,WarehouseId,QuantityOnHand,DamagedQuantity,ReorderLevel,Aisle,Shelf,Bin,ExpiryDate")] Inventory inventory)
        {
            // Ensure WarehouseId is set for WarehouseStaff before validation
            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.WarehouseId == null) return Forbid("Your account is not linked to a warehouse.");
                inventory.WarehouseId = user.WarehouseId.Value;
                // Clear validation for WarehouseId as it is not bound from the form
                ModelState.Remove("WarehouseId");
            }

            // Remove potential navigation validation keys
            ModelState.Remove("Product");
            ModelState.Remove("Warehouse");

            if (ModelState.IsValid)
            {
                _context.Add(inventory);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", inventory.ProductId);
            return View(inventory);
        }

        // GET: Inventories/Edit/5
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var inv = await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (inv == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user?.WarehouseId == null || inv.WarehouseId != user.WarehouseId) return Forbid();
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", inv.ProductId);
            return View(inv);
        }

        // POST: Inventories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProductId,WarehouseId,QuantityOnHand,DamagedQuantity,ReorderLevel,Aisle,Shelf,Bin,ExpiryDate")] Inventory inventory)
        {
            if (id != inventory.Id) return NotFound();
            var existing = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            if (existing == null) return NotFound();
            // Always preserve WarehouseId from existing to prevent tampering/overwrites
            // Also enforces that staff cannot change warehouses
            inventory.WarehouseId = existing.WarehouseId;
            // Clear validation for WarehouseId since it's preserved server-side
            ModelState.Remove("WarehouseId");
            // If ProductId didn't bind (e.g., disabled select), preserve existing and clear validation error
            if (inventory.ProductId == 0)
            {
                inventory.ProductId = existing.ProductId;
                ModelState.Remove("ProductId");
            }
            var user = await _userManager.GetUserAsync(User);
            if (user?.WarehouseId == null || existing.WarehouseId != user.WarehouseId) return Forbid();
            // Remove potential navigation validation keys
            ModelState.Remove("Product");
            ModelState.Remove("Warehouse");
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(inventory);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Inventories.AnyAsync(e => e.Id == inventory.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", inventory.ProductId);
            return View(inventory);
        }

        // GET: Inventories/Delete/5
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var inv = await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (inv == null) return NotFound();
            return View(inv);
        }

        // POST: Inventories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var inv = await _context.Inventories.FindAsync(id);
            if (inv != null)
            {
                _context.Inventories.Remove(inv);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
