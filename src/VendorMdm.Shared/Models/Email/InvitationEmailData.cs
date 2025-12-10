namespace VendorMdm.Shared.Models.Email
{
    public class InvitationEmailData
    {
        public string VendorName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string InvitationLink { get; set; } = string.Empty;
        public string ExpiresAt { get; set; } = string.Empty;
        public string InvitedByName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = "Vendor Portal";
    }
}
