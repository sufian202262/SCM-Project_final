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
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseStaff)]
    public class WarehouseTasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WarehouseTasksController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: WarehouseTasks
        public async Task<IActionResult> Index()
        {
            var query = _context.WarehouseTasks
                .Include(t => t.Product)
                .AsNoTracking();
            if (User.IsInRole(UserRoles.WarehouseStaff))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.WarehouseId == null) return Forbid("Your account is not linked to a warehouse.");
                query = query.Where(t => t.WarehouseId == user.WarehouseId);
            }
            var items = await query.OrderBy(t => t.Status).ThenBy(t => t.DueDate).Take(200).ToListAsync();
            return View(items);
        }

        // GET: WarehouseTasks/My
        public async Task<IActionResult> My()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var tasks = await _context.WarehouseTasks
                .Include(t => t.Product)
                .Where(t => t.AssignedToUserId == user.Id || (t.AssignedToUserId == null && t.WarehouseId == user.WarehouseId))
                .OrderBy(t => t.Status).ThenBy(t => t.DueDate)
                .Take(50)
                .ToListAsync();
            return View("Index", tasks);
        }

        // POST: WarehouseTasks/Start/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id)
        {
            var task = await _context.WarehouseTasks.FindAsync(id);
            if (task == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user?.WarehouseId == null || user.WarehouseId != task.WarehouseId) return Forbid();
            task.Status = WarehouseTaskStatus.InProgress;
            task.AssignedToUserId = user.Id;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Task started.";
            return RedirectToAction("Index");
        }

        // POST: WarehouseTasks/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var task = await _context.WarehouseTasks.FindAsync(id);
            if (task == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user?.WarehouseId == null || user.WarehouseId != task.WarehouseId) return Forbid();
            task.Status = WarehouseTaskStatus.Done;
            task.AssignedToUserId = user.Id;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Task completed.";
            return RedirectToAction("Index");
        }
    }
}
