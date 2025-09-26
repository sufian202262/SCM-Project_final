using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplyChainManagement.Data;
using SupplyChainManagement.Models;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Controllers
{
    [Authorize(Roles = UserRoles.Admin)]
    public class WarehousesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public WarehousesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Warehouses
        public async Task<IActionResult> Index()
        {
            var warehouses = await _context.Warehouses
                .Include(w => w.Inventories)
                .Include(w => w.Staff)
                .AsNoTracking()
                .OrderBy(w => w.Name)
                .ToListAsync();
            return View(warehouses);
        }

        // GET: Warehouses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var warehouse = await _context.Warehouses
                .Include(w => w.Inventories).ThenInclude(i => i.Product)
                .Include(w => w.Orders)
                .Include(w => w.Staff)
                .AsSplitQuery()
                .FirstOrDefaultAsync(w => w.Id == id);
            if (warehouse == null) return NotFound();
            return View(warehouse);
        }
    }
}
