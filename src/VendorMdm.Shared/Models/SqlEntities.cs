using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VendorMdm.Shared.Models;

public class ChangeRequest
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Submitted, Approved, Integrated

    public string? SapVendorId { get; set; } // Null for new vendors

    [Required]
    public Guid RequesterId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class VendorApplication
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TaxId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string ContactEmail { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";
    
    [MaxLength(20)]
    public string RegistrationType { get; set; } = "SelfRegistration";
    
    public Guid? InvitationId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class WorkflowState
{
    [Key]
    [MaxLength(20)]
    public string StateName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public class SapEnvironment
{
    [Key]
    [MaxLength(3)]
    public string EnvironmentCode { get; set; } = string.Empty; // D01, Q01, P01

    public string Description { get; set; } = string.Empty;
}

public class UserRole
{
    [Key]
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // Admin, Requester, Approver
}

public class Attachment
{
    [Key]
    public Guid Id { get; set; }
    public Guid LinkedEntityId { get; set; } // ChangeRequest or VendorApplication
    public string FileName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class VendorInvitation
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string InvitationToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string VendorLegalName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string PrimaryContactEmail { get; set; } = string.Empty;

    [Required]
    public Guid InvitedBy { get; set; }

    [Required]
    [MaxLength(200)]
    public string InvitedByName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpiresAt { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = InvitationStatus.Pending;

    public DateTime? CompletedAt { get; set; }

    public Guid? VendorApplicationId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public static class InvitationStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Expired = "Expired";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}
