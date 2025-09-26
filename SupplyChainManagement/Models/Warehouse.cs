using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SupplyChainManagement.Models
{
    public class Warehouse
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(200)]
        public string Location { get; set; } = string.Empty;
        
        [StringLength(20)]
        [Phone]
        public string? PhoneNumber { get; set; }
        
        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }
        
        // Navigation properties
        public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<ApplicationUser> Staff { get; set; } = new List<ApplicationUser>();
    }
}
