using Microsoft.AspNetCore.Identity;

namespace SupplyChainManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Personal information
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        // For supplier users
        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        
        // For warehouse staff users
        public int? WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }
        
        // Navigation properties
        public ICollection<Order> CreatedOrders { get; set; } = new List<Order>();
        public ICollection<Order> ApprovedOrders { get; set; } = new List<Order>();
        
        // Helper property to get full name
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
