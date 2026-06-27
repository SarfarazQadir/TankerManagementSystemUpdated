using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class TripEntryandTripLedgerModify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripExpenses_TripLedgers_TripLedgerId",
                table: "TripExpenses");

            migrationBuilder.AlterColumn<int>(
                name: "TripLedgerId",
                table: "TripExpenses",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "TripEntryId",
                table: "TripExpenses",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "04940bda-a780-43d2-b59c-52973c6c7273", "AQAAAAIAAYagAAAAEEwtCV5XVkLSzgwDfrogWpO2/EezBSSX90NJh0oENqub2nH21xGR+4Bl1jit8AFR1A==", "49cd3358-647d-4ee4-8f8c-9ac56ad5b5ec" });

            migrationBuilder.CreateIndex(
                name: "IX_TripExpenses_TripEntryId",
                table: "TripExpenses",
                column: "TripEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TripExpenses_TripEntries_TripEntryId",
                table: "TripExpenses",
                column: "TripEntryId",
                principalTable: "TripEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TripExpenses_TripLedgers_TripLedgerId",
                table: "TripExpenses",
                column: "TripLedgerId",
                principalTable: "TripLedgers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripExpenses_TripEntries_TripEntryId",
                table: "TripExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_TripExpenses_TripLedgers_TripLedgerId",
                table: "TripExpenses");

            migrationBuilder.DropIndex(
                name: "IX_TripExpenses_TripEntryId",
                table: "TripExpenses");

            migrationBuilder.DropColumn(
                name: "TripEntryId",
                table: "TripExpenses");

            migrationBuilder.AlterColumn<int>(
                name: "TripLedgerId",
                table: "TripExpenses",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "fa2e9193-aae4-417c-8a57-3c71e4b3b4f1", "AQAAAAIAAYagAAAAEMwz9aiyWZIKyf+puacYxkOY0Toin86hcuVCxSgO2zdBnN6rK69q9XzOGcC024GmVQ==", "bbf6d684-3857-4c75-9069-4675d58ed873" });

            migrationBuilder.AddForeignKey(
                name: "FK_TripExpenses_TripLedgers_TripLedgerId",
                table: "TripExpenses",
                column: "TripLedgerId",
                principalTable: "TripLedgers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
