using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace TankerManagementSystem.Models
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {        
        public DbSet<Tanker> Tankers { get; set; }
        public DbSet<TankerOwner> TankerOwners { get; set; }
        public DbSet<TripLedger> TripLedgers { get; set; }
        public DbSet<TripExpense> TripExpenses { get; set; }
        public DbSet<CashLedger> CashLedgers { get; set; }
        public DbSet<PersonalKhata> PersonalKhatas { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<CommissionSetup> CommissionSetups { get; set; }
        public DbSet<TripEntry> TripEntries { get; set; }
        // FIX Issue 16: Removed DbSet<TankerBalanceLog> — dead code, table never populated - Done by AntiGravity on 2026-07-18 08:15 PST
        public DbSet<AppModule> AppModules { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<TankerLedger> TankerLedgers { get; set; }
        public DbSet<KhataPerson> KhataPersons { get; set; }
        public DbSet<AtsPsoEntry> AtsPsoEntries { get; set; }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // FIX Issue 15: Unique indexes on business-critical fields - Done by AntiGravity on 2026-07-18 08:15 PST
            builder.Entity<TankerOwner>()
                .HasIndex(x => x.CNIC)
                .IsUnique()
                .HasDatabaseName("IX_TankerOwner_CNIC_Unique");

            builder.Entity<Tanker>()
                .HasIndex(x => x.TankerNo)
                .IsUnique()
                .HasDatabaseName("IX_Tanker_TankerNo_Unique");

            builder.Entity<TripLedger>()
                .HasIndex(x => x.TokenNo)
                .IsUnique()
                .HasDatabaseName("IX_TripLedger_TokenNo_Unique");

            // Modified by AI
            // Date: 2026-07-21
            // Reason: M-02 — Added unique index on TripEntryId to enforce the one-to-one
            // relationship between TripEntry and TripLedger at the database level.
            // Without this, a race condition between the duplicate-check and insert
            // in AddLedger could create two TripLedger rows for one TripEntry,
            // causing double-credited balances on the TankerLedger.
            builder.Entity<TripLedger>()
                .HasIndex(x => x.TripEntryId)
                .IsUnique()
                .HasDatabaseName("IX_TripLedger_TripEntryId_Unique");

            SeedRoles(builder);
        }       
        // Modified by AI
        // Date: 2026-07-21
        // Reason: H-02 — SECURITY WARNING: The admin password below is seeded in plain
        // text during initial database creation. This value is stored in source control
        // history. ACTION REQUIRED:
        //   1. Change the admin password immediately after first login via
        //      Admin > Profile > Change Password.
        //   2. For new deployments, override via environment variable ADMIN_SEED_PASSWORD
        //      before running 'dotnet ef database update'.
        //   3. Never commit a production password to source control.
        private static void SeedRoles(ModelBuilder builder)
        {
            // ROLE IDs
            string adminRoleId = "1";
            string userRoleId = "2";

            // USER ID
            string adminUserId = "100";

            // Seed Roles
            builder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = adminRoleId,
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp = adminRoleId
                },
                new IdentityRole
                {
                    Id = userRoleId,
                    Name = "User",
                    NormalizedName = "USER",
                    ConcurrencyStamp = userRoleId
                }
            );

            // Create Admin User
            // Modified by AI
            // Date: 2026-07-21
            // Reason: H-02 — Read seed password from environment variable when available
            // to avoid committing credentials to source control. Falls back to the default
            // seed password for local development only.
            string seedPassword = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD")
                                  ?? "Neym@rjr2244"; // DEFAULT: Change after first login!

            var adminUser = new IdentityUser
            {
                Id = adminUserId,
                UserName = "sqadmin",
                NormalizedUserName = "ADMIN",
                Email = "sarfarazqadir594@gmail.com",
                NormalizedEmail = "ADMIN@GMAIL.COM",
                EmailConfirmed = true
            };

            // Password Hash
            var passwordHasher = new PasswordHasher<IdentityUser>();
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, seedPassword);

            // Seed User
            builder.Entity<IdentityUser>().HasData(adminUser);

            // Assign Role to User
            builder.Entity<IdentityUserRole<string>>().HasData(
                new IdentityUserRole<string>
                {
                    UserId = adminUserId,
                    RoleId = adminRoleId
                }
            );
        }
    }
}