using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace TankerManagementSystem.Models
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        //public DbSet<Admin> tbl_admin { get; set; }
        //public DbSet<User> Users { get; set; }
        //public DbSet<Role> Roles { get; set; }
        //public DbSet<AppModule> AppModules { get; set; }
        //public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<Tanker> Tankers { get; set; }
        public DbSet<TankerOwner> TankerOwners { get; set; }
        public DbSet<TripLedger> TripLedgers { get; set; }
        public DbSet<TripExpense> TripExpenses { get; set; }
        public DbSet<CashLedger> CashLedgers { get; set; }
        public DbSet<PersonalKhata> PersonalKhatas { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<CommissionSetup> CommissionSetups { get; set; }
        public DbSet<TripEntry> TripEntries { get; set; }
        public DbSet<TankerBalanceLog> TankerBalanceLogs { get; set; }
        public DbSet<AppModule> AppModules { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<TankerLedger> TankerLedgers { get; set; }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            SeedRoles(builder);
        }
        /* private static void SeedRoles(ModelBuilder builder)
        {
            builder.Entity<IdentityRole>().HasData
                (
                new IdentityRole() { Name = "Admin", ConcurrencyStamp = "1", NormalizedName = "Admin" },
                new IdentityRole() { Name = "User", ConcurrencyStamp = "2", NormalizedName = "User" }
                );
            builder.Entity<IdentityUser>().HasData
                (
                new IdentityUser() { Id = "1", UserName = "admin", Email = "admin@gmail.com", PasswordHash = "Admin@123" }
                );
        }*/
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
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Neym@rjr2244");

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