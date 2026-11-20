using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using DearHome_Backend.Models;
using DearHome_Backend.Constants;
using Microsoft.EntityFrameworkCore.Diagnostics;
using DearHome_Backend.Data.DataConfigs;
using Microsoft.EntityFrameworkCore.Infrastructure;


namespace DearHome_Backend.Data;

public class DearHomeContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public DearHomeContext(DbContextOptions<DearHomeContext> options)
        : base(options)
    {
    }
    public DbSet<VerificationCode> VerificationCodes { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Models.Attribute> Attributes { get; set; }
    public DbSet<AttributeValue> AttributeValues { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<CategoryAttribute> CategoryAttributes { get; set; }
    public DbSet<Combo> Combos { get; set; }
    public DbSet<ProductCombo> ProductCombos { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<GoodReceivedNote> GoodReceivedNotes { get; set; }
    public DbSet<GoodReceivedItem> GoodReceivedItems { get; set; }
    public DbSet<Variant> Variants { get; set; }
    public DbSet<VariantAttribute> VariantAttributes { get; set; }
    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<Placement> Placements { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    /// <summary>
    /// Every decimal column is decimal(18,2) (money amounts, percentages, ratings). This is the store type SQL Server
    /// already used by default, so existing databases are unchanged; declaring it removes EF's truncation warnings.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

   protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Example: modelBuilder.ApplyConfiguration(new UserConfiguration());

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new VariantConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new VariantAttributeConfiguration());
        modelBuilder.ApplyConfiguration(new VariantConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryAttributeConfiguration());
        modelBuilder.ApplyConfiguration(new ReviewConfiguration());
        modelBuilder.ApplyConfiguration(new OrderDetailConfiguration());

        SeedData(modelBuilder);
    }

    // Seed data must be deterministic: HasData values are compared against the migration snapshot, so random ids,
    // stamps or password hashes would make every new migration delete and re-insert the admin account.
    // The ids below are the ones recorded in the InitialCreate migration; do not change them.
    private static readonly Guid SeedAdminUserId = new("05f43c50-0795-4cb7-b4d9-ab542b9938f1");
    private static readonly Guid SeedAdminRoleId = new("3ba70d13-71cd-49de-b153-9482c6310aed");
    private static readonly Guid SeedUserRoleId = new("086ac42c-0549-4078-9be9-d2e0666f339b");
    private const string SeedAdminConcurrencyStamp = "d18ab30c-6fef-4309-aaa0-929c5120448c";

    // ASP.NET Core Identity v3 hash of the default admin password "Admin@123" (change it after the first login).
    private const string SeedAdminPasswordHash =
        "AQAAAAIAAYagAAAAEJNfiroKOnSiUBnTk8dcJBMcPCWdO6ZP34APym11WVFEfw0jlEJVsfZcBXK12tryMg==";

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid> { Id = SeedAdminRoleId, Name = UserRole.Admin.ToString(), NormalizedName = UserRole.Admin.ToString().ToUpper() },
            new IdentityRole<Guid> { Id = SeedUserRoleId, Name = UserRole.User.ToString(), NormalizedName = UserRole.User.ToString().ToUpper() }
        );

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = SeedAdminUserId,
                UserName = "admin",
                NormalizedUserName = "ADMIN",
                Email = "admin@gmail.com",
                Name = "Admin User",
                PhoneNumber = "1234567890",
                PasswordHash = SeedAdminPasswordHash,
                ConcurrencyStamp = SeedAdminConcurrencyStamp,
            }
        );

        modelBuilder.Entity<IdentityUserRole<Guid>>().HasData(
            new IdentityUserRole<Guid>
            {
                UserId = SeedAdminUserId,
                RoleId = SeedAdminRoleId
            }
        );
    }
}
