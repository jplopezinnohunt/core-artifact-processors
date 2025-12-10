using Microsoft.EntityFrameworkCore;
using VendorMdm.Shared.Models;

namespace VendorMdm.Artifacts.Data;

public class ArtifactDbContext : DbContext
{
    public ArtifactDbContext(DbContextOptions<ArtifactDbContext> options) : base(options) { }

    public DbSet<ChangeRequest> ChangeRequests { get; set; }
    public DbSet<VendorApplication> VendorApplications { get; set; }
    public DbSet<WorkflowState> WorkflowStates { get; set; }
    public DbSet<SapEnvironment> SapEnvironments { get; set; }
    public DbSet<UserRole> UsersAndRoles { get; set; }
    public DbSet<Attachment> Attachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configuration matches the API's DbContext to ensure compatibility
    }
}
