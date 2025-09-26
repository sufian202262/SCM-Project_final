using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupplyChainManagement.Data;
using SupplyChainManagement.Models;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();
        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "First name")]
            public string FirstName { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Last name")]
            public string LastName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, ErrorMessage = "{0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Phone]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }

            [Required]
            [Display(Name = "Role")]
            public string Role { get; set; } = UserRoles.Supplier;

            // New supplier details (when role is Supplier)
            [Display(Name = "Company name")]
            public string? SupplierCompanyName { get; set; }
            [Display(Name = "Contact name")]
            public string? SupplierContactName { get; set; }
            [EmailAddress]
            [Display(Name = "Supplier email")]
            public string? SupplierEmail { get; set; }
            [Phone]
            [Display(Name = "Supplier phone")]
            public string? SupplierPhone { get; set; }
            [Display(Name = "Supplier address")]
            public string? SupplierAddress { get; set; }
            [Display(Name = "Supplier website")]
            public string? SupplierWebsite { get; set; }

            // New warehouse details (when role is WarehouseStaff)
            [Display(Name = "Warehouse name")]
            public string? WarehouseName { get; set; }
            [Display(Name = "Warehouse location")]
            public string? WarehouseLocation { get; set; }
            [Phone]
            [Display(Name = "Warehouse phone")]
            public string? WarehousePhoneNumber { get; set; }
            [EmailAddress]
            [Display(Name = "Warehouse email")]
            public string? WarehouseEmail { get; set; }
        }

        public SelectList RoleOptions { get; set; } = null!;

        public async Task OnGetAsync(string? returnUrl = null)
        {
            // If already authenticated, redirect to home
            if (User?.Identity?.IsAuthenticated ?? false)
            {
                Response.Redirect("~/");
                return;
            }
            ReturnUrl = returnUrl;
            await LoadOptionsAsync();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
            if (!ModelState.IsValid)
            {
                await LoadOptionsAsync();
                return Page();
            }

            // Prevent Admin registrations via public form
            if (Input.Role == UserRoles.Admin)
            {
                ModelState.AddModelError(string.Empty, "Admin accounts cannot be registered here.");
                await LoadOptionsAsync();
                return Page();
            }

            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                FirstName = Input.FirstName,
                LastName = Input.LastName,
                PhoneNumber = Input.PhoneNumber,
                SupplierId = null,
                WarehouseId = null
            };

            // Link supplier/warehouse based on chosen role
            if (Input.Role == UserRoles.Supplier)
            {
                if (string.IsNullOrWhiteSpace(Input.SupplierCompanyName))
                {
                    ModelState.AddModelError("Input.SupplierCompanyName", "Company name is required for supplier registration.");
                    await LoadOptionsAsync();
                    return Page();
                }
                var supplier = new Supplier
                {
                    CompanyName = Input.SupplierCompanyName!.Trim(),
                    Name = Input.SupplierContactName,
                    Email = Input.SupplierEmail,
                    Phone = Input.SupplierPhone,
                    Address = Input.SupplierAddress,
                    Website = Input.SupplierWebsite
                };
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();
                user.SupplierId = supplier.Id;
            }
            if (Input.Role == UserRoles.WarehouseStaff)
            {
                if (string.IsNullOrWhiteSpace(Input.WarehouseName) || string.IsNullOrWhiteSpace(Input.WarehouseLocation))
                {
                    if (string.IsNullOrWhiteSpace(Input.WarehouseName))
                        ModelState.AddModelError("Input.WarehouseName", "Warehouse name is required for warehouse staff registration.");
                    if (string.IsNullOrWhiteSpace(Input.WarehouseLocation))
                        ModelState.AddModelError("Input.WarehouseLocation", "Warehouse location is required for warehouse staff registration.");
                    await LoadOptionsAsync();
                    return Page();
                }
                var warehouse = new Warehouse
                {
                    Name = Input.WarehouseName!.Trim(),
                    Location = Input.WarehouseLocation!.Trim(),
                    PhoneNumber = Input.WarehousePhoneNumber,
                    Email = Input.WarehouseEmail
                };
                _context.Warehouses.Add(warehouse);
                await _context.SaveChangesAsync();
                user.WarehouseId = warehouse.Id;
            }
            var result = await _userManager.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {
                // Ensure role exists then assign
                if (!await _roleManager.RoleExistsAsync(Input.Role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(Input.Role));
                }
                await _userManager.AddToRoleAsync(user, Input.Role);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(ReturnUrl);
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        private async Task LoadOptionsAsync()
        {
            RoleOptions = new SelectList(new[] { UserRoles.Supplier, UserRoles.WarehouseStaff });
        }
    }
}
