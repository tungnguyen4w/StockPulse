using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VIN = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: false),
                    Make = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ArrivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAging = table.Column<bool>(type: "bit", nullable: false),
                    DealershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleActionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleActionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleActionLogs_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleActionLogs_VehicleId",
                table: "VehicleActionLogs",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ArrivedAt",
                table: "Vehicles",
                column: "ArrivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DealershipId",
                table: "Vehicles",
                column: "DealershipId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_IsAging",
                table: "Vehicles",
                column: "IsAging");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Make",
                table: "Vehicles",
                column: "Make");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Model",
                table: "Vehicles",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VIN",
                table: "Vehicles",
                column: "VIN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Year",
                table: "Vehicles",
                column: "Year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleActionLogs");

            migrationBuilder.DropTable(
                name: "Vehicles");
        }
    }
}
