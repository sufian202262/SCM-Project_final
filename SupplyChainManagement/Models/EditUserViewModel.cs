using System.ComponentModel.DataAnnotations;

namespace SupplyChainManagement.Models
{
    public class EditUserViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Role")]
        [Required]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "New Password")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        public string? NewPassword { get; set; }

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
