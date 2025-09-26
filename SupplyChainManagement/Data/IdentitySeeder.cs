using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupplyChainManagement.Models;
using SupplyChainManagement.Models.Enums;

namespace SupplyChainManagement.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceScope scope)
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Capture initial state BEFORE we seed anything so we can decide if this is the first run
            var hadAnyUsers = await userManager.Users.AnyAsync();
            var hadAnySuppliers = await context.Suppliers.AnyAsync();

            // Ensure roles exist
            foreach (var role in new[] { UserRoles.Admin, UserRoles.WarehouseStaff, UserRoles.Supplier })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Seed an admin user (credentials from configuration)
            var adminEmail = config["Seed:Admin:Email"] ?? "admin@example.com";
            var adminPassword = config["Seed:Admin:Password"] ?? "Admin1234";

            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User"
                };
                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, UserRoles.Admin);
                }
            }

            // Seed a test supplier user ONLY on the very first run when DB was empty
            var supplierEmail = "supplier@example.com";
            var supplierPassword = "Supplier123!";

            var supplier = await userManager.FindByEmailAsync(supplierEmail);
            if (supplier == null && !hadAnyUsers && !hadAnySuppliers)
            {
                // First, ensure we have a supplier record
                var supplierRecord = await context
                    .Suppliers.FirstOrDefaultAsync(s => s.Name == "Test Supplier");
                
                if (supplierRecord == null)
                {
                    supplierRecord = new Supplier
                    {
                        Name = "Test Supplier",
                        Email = supplierEmail,
                        Phone = "555-0123",
                        Address = "123 Supplier St, Supply City, SC 12345",
                    };
                    context.Suppliers.Add(supplierRecord);
                    await context.SaveChangesAsync();
                }

                supplier = new ApplicationUser
                {
                    UserName = supplierEmail,
                    Email = supplierEmail,
                    EmailConfirmed = true,
                    FirstName = "Test",
                    LastName = "Supplier",
                    SupplierId = supplierRecord.Id
                };
                var result = await userManager.CreateAsync(supplier, supplierPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(supplier, UserRoles.Supplier);
                }
            }
        }
    }
}
