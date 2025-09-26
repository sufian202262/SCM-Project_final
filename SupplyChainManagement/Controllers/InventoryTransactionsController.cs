using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplyChainManagement.Data;
using SupplyChainManagement.Models;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Controllers
{
    // Read-only browsing of inventory transactions
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseStaff)]
    public class InventoryTransactionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InventoryTransactionsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: InventoryTransactions
        public async Task<IActionResult> Index(int? warehouseId, int? productId, DateTime? from, DateTime? to, InventoryTransactionType? type)
        {
            var query = _context.InventoryTransactions
                .Include(t => t.Product)
                .Include(t => t.Warehouse)
                .AsNoTracking()
                .AsQueryable();

            // WarehouseStaff limited to their warehouse
            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.WarehouseId == null) return Forbid("Your account is not linked to a warehouse.");
                query = query.Where(t => t.WarehouseId == user.WarehouseId.Value);
            }
            else if (warehouseId.HasValue)
            {
                query = query.Where(t => t.WarehouseId == warehouseId.Value);
            }

            if (productId.HasValue)
            {
                query = query.Where(t => t.ProductId == productId.Value);
            }
            if (type.HasValue)
            {
                query = query.Where(t => t.Type == type.Value);
            }
            if (from.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= from.Value);
            }
            if (to.HasValue)
            {
                // include the end day fully
                var end = to.Value.Date.AddDays(1);
                query = query.Where(t => t.CreatedAt < end);
            }

            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Take(500) // simple cap to keep page light
                .ToListAsync();

            ViewData["WarehouseId"] = warehouseId;
            ViewData["ProductId"] = productId;
            ViewData["From"] = from?.ToString("yyyy-MM-dd");
            ViewData["To"] = to?.ToString("yyyy-MM-dd");
            ViewData["Type"] = type;

            return View(items);
        }

        // GET: InventoryTransactions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var t = await _context.InventoryTransactions
                .Include(x => x.Product)
                .Include(x => x.Warehouse)
                .Include(x => x.PerformedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();

            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.WarehouseId == null || t.WarehouseId != user.WarehouseId) return Forbid();
            }

            return View(t);
        }
    }
}
