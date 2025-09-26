using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplyChainManagement.Data;
using SupplyChainManagement.Models;
using SupplyChainManagement.Models.Enums;
using System.Linq;
using System.Threading.Tasks;

namespace SupplyChainManagement.Controllers
{
    [Authorize(Roles = UserRoles.Admin)]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRolesViewModel = new List<UserRolesViewModel>();
            foreach (ApplicationUser user in users)
            {
                var thisViewModel = new UserRolesViewModel();
                thisViewModel.UserId = user.Id;
                thisViewModel.Email = user.Email ?? string.Empty;
                thisViewModel.FirstName = user.FirstName;
                thisViewModel.LastName = user.LastName;
                thisViewModel.Roles = await GetUserRoles(user);
                
                
                userRolesViewModel.Add(thisViewModel);
            }
            return View(userRolesViewModel);
        }

        private async Task<List<string>> GetUserRoles(ApplicationUser user)
        {
            return new List<string>(await _userManager.GetRolesAsync(user));
        }

        public async Task<IActionResult> Create(string? role)
        {
            ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            var model = new CreateUserViewModel();
            if (!string.IsNullOrWhiteSpace(role))
            {
                // Normalize incoming role to known roles if possible
                if (string.Equals(role, UserRoles.Supplier, System.StringComparison.OrdinalIgnoreCase))
                    model.Role = UserRoles.Supplier;
                else if (string.Equals(role, UserRoles.WarehouseStaff, System.StringComparison.OrdinalIgnoreCase))
                    model.Role = UserRoles.WarehouseStaff;
                else if (string.Equals(role, UserRoles.Admin, System.StringComparison.OrdinalIgnoreCase))
                    model.Role = UserRoles.Admin;
                else
                    model.Role = role; // fallback
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(model);
            }

            var user = new ApplicationUser 
            { 
                UserName = model.Email, 
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };



            var result = await _userManager.CreateAsync(user, model.Password);
            
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);

                // Auto-create and link Supplier or Warehouse based on selected role
                if (model.Role == UserRoles.Supplier)
                {
                    var supplier = new Supplier
                    {
                        CompanyName = string.IsNullOrWhiteSpace(model.CompanyName) ? (user.FullName?.Trim().Length > 0 ? user.FullName : user.Email ?? "Supplier") : model.CompanyName!,
                        Name = (user.FullName?.Trim()) ?? user.Email,
                        Email = user.Email,
                        Phone = model.Phone,
                        Address = model.Address,
                        Website = model.Website
                    };
                    _context.Suppliers.Add(supplier);
                    await _context.SaveChangesAsync();
                    user.SupplierId = supplier.Id;
                    await _userManager.UpdateAsync(user);
                }
                else if (model.Role == UserRoles.WarehouseStaff)
                {
                    var warehouse = new Warehouse
                    {
                        Name = string.IsNullOrWhiteSpace(model.WarehouseName) ? $"Warehouse of {(user.FullName?.Trim().Length > 0 ? user.FullName : user.Email)}" : model.WarehouseName!,
                        Location = model.WarehouseLocation ?? "",
                        Email = user.Email
                    };
                    _context.Warehouses.Add(warehouse);
                    await _context.SaveChangesAsync();
                    user.WarehouseId = warehouse.Id;
                    await _userManager.UpdateAsync(user);
                }
                TempData["Success"] = $"User '{model.Email}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.Suppliers = await _context.Suppliers.Select(s => new { s.Id, s.Name }).ToListAsync();
            ViewBag.Warehouses = await _context.Warehouses.Select(w => new { w.Id, w.Name }).ToListAsync();
            return View(model);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            
            var vm = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Role = roles.FirstOrDefault() ?? string.Empty
            };
            return View(vm);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(model);
            }
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            // Update user properties
            user.Email = model.Email;
            user.UserName = model.Email;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            
            var updateRes = await _userManager.UpdateAsync(user);
            if (!updateRes.Succeeded)
            {
                foreach (var err in updateRes.Errors) ModelState.AddModelError(string.Empty, err.Description);
                ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(model);
            }

            // Update role (single role model)
            var currentRoles = await _userManager.GetRolesAsync(user);
            var desiredRole = model.Role;
            var rolesToRemove = currentRoles.Where(r => r != desiredRole).ToList();
            if (rolesToRemove.Any()) await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!string.IsNullOrWhiteSpace(desiredRole) && !currentRoles.Contains(desiredRole))
            {
                await _userManager.AddToRoleAsync(user, desiredRole);
            }

            // Update password if provided
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                // Remove and add new password
                var hasPassword = await _userManager.HasPasswordAsync(user);
                IdentityResult passRes;
                if (hasPassword)
                {
                    passRes = await _userManager.RemovePasswordAsync(user);
                    if (passRes.Succeeded)
                        passRes = await _userManager.AddPasswordAsync(user, model.NewPassword);
                }
                else
                {
                    passRes = await _userManager.AddPasswordAsync(user, model.NewPassword);
                }
                if (!passRes.Succeeded)
                {
                    foreach (var err in passRes.Errors) ModelState.AddModelError(string.Empty, err.Description);
                    ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
                    return View(model);
                }
            }

            TempData["Success"] = "User updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var vm = new UserRolesViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Roles = await GetUserRoles(user)
            };
            return View(vm);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // If user is linked to a Supplier, attempt to delete the Supplier record too
            if (user.SupplierId != null)
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.Products)
                    .Include(s => s.Orders)
                    .FirstOrDefaultAsync(s => s.Id == user.SupplierId);

                if (supplier != null)
                {
                    var hasDependencies = (supplier.Products?.Any() ?? false) || (supplier.Orders?.Any() ?? false);
                    if (hasDependencies)
                    {
                        TempData["Error"] = "Cannot delete this user because the linked supplier has products or orders. Remove or reassign them first.";
                        return RedirectToAction(nameof(Delete), new { id });
                    }

                    _context.Suppliers.Remove(supplier);
                    await _context.SaveChangesAsync();
                }
            }

            // If user is linked to a Warehouse, attempt to delete the Warehouse record too
            if (user.WarehouseId != null)
            {
                var warehouse = await _context.Warehouses
                    .Include(w => w.Inventories)
                    .Include(w => w.Orders)
                    .Include(w => w.Staff)
                    .FirstOrDefaultAsync(w => w.Id == user.WarehouseId);

                if (warehouse != null)
                {
                    var hasInventoriesOrOrders = (warehouse.Inventories?.Any() ?? false) || (warehouse.Orders?.Any() ?? false);
                    var hasOtherStaff = warehouse.Staff?.Any(s => s.Id != user.Id) ?? false;
                    if (hasInventoriesOrOrders || hasOtherStaff)
                    {
                        TempData["Error"] = "Cannot delete this user because the linked warehouse has inventories, orders, or other staff. Remove or reassign them first.";
                        return RedirectToAction(nameof(Delete), new { id });
                    }

                    _context.Warehouses.Remove(warehouse);
                    await _context.SaveChangesAsync();
                }
            }

            var res = await _userManager.DeleteAsync(user);
            if (!res.Succeeded)
            {
                foreach (var err in res.Errors) ModelState.AddModelError(string.Empty, err.Description);
                var vm = new UserRolesViewModel
                {
                    UserId = id,
                    Email = user.Email ?? string.Empty,
                    Roles = await GetUserRoles(user)
                };
                return View("Delete", vm);
            }
            TempData["Success"] = "User deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
