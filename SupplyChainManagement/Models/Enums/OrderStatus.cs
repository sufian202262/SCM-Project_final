namespace SupplyChainManagement.Models.Enums
{
    public enum OrderStatus
    {
        Draft = 0,
        Pending = 1,
        PendingApproval = 2,
        Approved = 3,
        Processing = 4,
        Shipped = 5,
        Delivered = 6,
        Cancelled = 7,
        Rejected = 8,
        SentToSupplier = 9,
        ConfirmedBySupplier = 10
    }
}
