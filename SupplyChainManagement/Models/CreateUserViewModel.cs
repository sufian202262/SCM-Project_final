using System.ComponentModel.DataAnnotations;

namespace SupplyChainManagement.Models
{
    public class CreateUserViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        // For Supplier role
        [StringLength(100)]
        public string? CompanyName { get; set; }
        
        [StringLength(200)]
        public string? Address { get; set; }
        
        [StringLength(20)]
        [Phone]
        public string? Phone { get; set; }
        
        [StringLength(100)]
        public string? Website { get; set; }
        
        // For WarehouseStaff role
        [StringLength(100)]
        public string? WarehouseName { get; set; }
        
        [StringLength(200)]
        public string? WarehouseLocation { get; set; }
    }
}
