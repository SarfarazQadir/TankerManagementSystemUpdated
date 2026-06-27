using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "84ee2ea6-5289-4e06-826f-263f335fb008");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a4b8aaa5-d310-4e41-ac35-90b3b237a247");

            migrationBuilder.CreateTable(
                name: "AppModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Controller = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppModules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashLedgers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionSetups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionSetups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalKhatas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AddAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinusAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalKhatas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TankerOwners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CNIC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TankerOwners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanCreate = table.Column<bool>(type: "bit", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_AppModules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "AppModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tankers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TankerNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Capacity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tankers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tankers_TankerOwners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "TankerOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TankerBalanceLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TankerId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenceId = table.Column<int>(type: "int", nullable: true),
                    PreviousBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TankerBalanceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TankerBalanceLogs_Tankers_TankerId",
                        column: x => x.TankerId,
                        principalTable: "Tankers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TankerId = table.Column<int>(type: "int", nullable: false),
                    AdvanceCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FromLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripEntries_Tankers_TankerId",
                        column: x => x.TankerId,
                        principalTable: "Tankers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TripEntryId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Freight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdvanceCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Shortage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Commission = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Munshiana = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPay = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TokenNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripLedgers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripLedgers_TripEntries_TripEntryId",
                        column: x => x.TripEntryId,
                        principalTable: "TripEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripLedgerId = table.Column<int>(type: "int", nullable: false),
                    ExpenseName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripExpenses_TripLedgers_TripLedgerId",
                        column: x => x.TripLedgerId,
                        principalTable: "TripLedgers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "1", "1", "Admin", "ADMIN" },
                    { "2", "2", "User", "USER" }
                });

            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[] { "100", 0, "221cefd7-0b8d-42dc-86f4-071e26accb45", "sarfarazqadir594@gmail.com", true, false, null, "ADMIN@GMAIL.COM", "ADMIN", "AQAAAAIAAYagAAAAECbzy8KOxStW+l5SSAHx3zBSMzY/bMckEif9dmdtX+bqW1w3FRLFNPFsuvlxcDZOwQ==", null, false, "69cbaad1-79b7-4a58-9fe7-28e7b3f97233", false, "sqadmin" });

            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[] { "1", "100" });

            migrationBuilder.CreateIndex(
                name: "IX_TankerBalanceLogs_TankerId",
                table: "TankerBalanceLogs",
                column: "TankerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tankers_OwnerId",
                table: "Tankers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TripEntries_TankerId",
                table: "TripEntries",
                column: "TankerId");

            migrationBuilder.CreateIndex(
                name: "IX_TripExpenses_TripLedgerId",
                table: "TripExpenses",
                column: "TripLedgerId");

            migrationBuilder.CreateIndex(
                name: "IX_TripLedgers_ProductId",
                table: "TripLedgers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TripLedgers_TripEntryId",
                table: "TripLedgers",
                column: "TripEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_ModuleId",
                table: "UserPermissions",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId",
                table: "UserPermissions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashLedgers");

            migrationBuilder.DropTable(
                name: "CommissionSetups");

            migrationBuilder.DropTable(
                name: "PersonalKhatas");

            migrationBuilder.DropTable(
                name: "TankerBalanceLogs");

            migrationBuilder.DropTable(
                name: "TripExpenses");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "TripLedgers");

            migrationBuilder.DropTable(
                name: "AppModules");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "TripEntries");

            migrationBuilder.DropTable(
                name: "Tankers");

            migrationBuilder.DropTable(
                name: "TankerOwners");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2");

            migrationBuilder.DeleteData(
                table: "AspNetUserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { "1", "100" });

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "84ee2ea6-5289-4e06-826f-263f335fb008", "1", "Admin", "Admin" },
                    { "a4b8aaa5-d310-4e41-ac35-90b3b237a247", "2", "User", "User" }
                });
        }
    }
}
