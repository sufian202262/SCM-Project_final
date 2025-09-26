using System.Collections.Generic;

namespace SupplyChainManagement.Models
{
    public class UserRolesViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public IEnumerable<string> Roles { get; set; } = new List<string>();
        public string? SupplierName { get; set; }
        public string? WarehouseName { get; set; }
    }
}
