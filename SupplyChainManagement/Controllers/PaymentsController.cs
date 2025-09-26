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
    [Authorize]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PaymentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // POST: Payments/Start
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.WarehouseStaff)]
        public async Task<IActionResult> Start(int orderId, PaymentMethod method)
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

            var amount = order.Items?.Sum(i => i.UnitPrice * i.Quantity) ?? 0m;

            var payment = new Payment
            {
                OrderId = order.Id,
                Amount = amount,
                Currency = "BDT",
                Method = method,
                Status = PaymentStatus.Pending,
                Gateway = "SSLCommerz",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id,
                Notes = "Initiated"
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // TODO: Replace with redirect to actual gateway URL.
            // For now, simulate immediate success and redirect back.
            return RedirectToAction(nameof(Success), new { id = payment.Id });
        }

        // GET: Payments/Success/{id}
        [HttpGet]
        public async Task<IActionResult> Success(int id)
        {
            var payment = await _context.Payments.Include(p => p.Order).FirstOrDefaultAsync(p => p.Id == id);
            if (payment == null) return NotFound();

            payment.Status = PaymentStatus.Captured;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.Notes = (payment.Notes ?? "") + "\nCaptured (sandbox)";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment successful.";
            return RedirectToAction("Details", "Orders", new { id = payment.OrderId });
        }

        // GET: Payments/Fail/{id}
        [HttpGet]
        public async Task<IActionResult> Fail(int id)
        {
            var payment = await _context.Payments.Include(p => p.Order).FirstOrDefaultAsync(p => p.Id == id);
            if (payment == null) return NotFound();
            payment.Status = PaymentStatus.Failed;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.Notes = (payment.Notes ?? "") + "\nFailed (sandbox)";
            await _context.SaveChangesAsync();
            TempData["Error"] = "Payment failed.";
            return RedirectToAction("Details", "Orders", new { id = payment.OrderId });
        }

        // GET: Payments/Cancel/{id}
        [HttpGet]
        public async Task<IActionResult> Cancel(int id)
        {
            var payment = await _context.Payments.Include(p => p.Order).FirstOrDefaultAsync(p => p.Id == id);
            if (payment == null) return NotFound();
            payment.Status = PaymentStatus.Cancelled;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.Notes = (payment.Notes ?? "") + "\nCancelled (sandbox)";
            await _context.SaveChangesAsync();
            TempData["Error"] = "Payment cancelled.";
            return RedirectToAction("Details", "Orders", new { id = payment.OrderId });
        }
    }
}
