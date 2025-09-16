using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ecare_Kiosk_Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SapOk = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ecare_Kiosk_Lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Occupied = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ecare_Kiosk_Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slv = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ecare_Kiosk_Drivers_Ecare_Kiosk_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Ecare_Kiosk_Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ecare_Kiosk_Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductType = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ecare_Kiosk_Orders_Ecare_Kiosk_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Ecare_Kiosk_Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ecare_Kiosk_Orders_Ecare_Kiosk_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Ecare_Kiosk_Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ecare_Kiosk_Weighings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    GrossKg = table.Column<int>(type: "int", nullable: false),
                    TareKg = table.Column<int>(type: "int", nullable: false),
                    NetKg = table.Column<int>(type: "int", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weighings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ecare_Kiosk_Weighings_Ecare_Kiosk_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Ecare_Kiosk_Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ecare_Kiosk_BonLivraison",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NetKg = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonLivraison", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ecare_Kiosk_BonLivraison_Ecare_Kiosk_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Ecare_Kiosk_Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ecare_Kiosk_BonLivraison_BlNumber",
                table: "Ecare_Kiosk_BonLivraison",
                column: "BlNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ecare_Kiosk_BonLivraison_OrderId",
                table: "Ecare_Kiosk_BonLivraison",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Ecare_Kiosk_Drivers_ClientId",
                table: "Ecare_Kiosk_Drivers",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Ecare_Kiosk_Drivers_Slv",
                table: "Ecare_Kiosk_Drivers",
                column: "Slv",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ecare_Kiosk_Orders_ClientId",
                table: "Ecare_Kiosk_Orders",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Ecare_Kiosk_Orders_DriverId",
                table: "Ecare_Kiosk_Orders",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Ecare_Kiosk_Orders_Number",
                table: "Ecare_Kiosk_Orders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ecare_Kiosk_Weighings_OrderId",
                table: "Ecare_Kiosk_Weighings",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ecare_Kiosk_BonLivraison");

            migrationBuilder.DropTable(
                name: "Ecare_Kiosk_Weighings");

            migrationBuilder.DropTable(
                name: "Ecare_Kiosk_Orders");

            migrationBuilder.DropTable(
                name: "Ecare_Kiosk_Drivers");

            migrationBuilder.DropTable(
                name: "Ecare_Kiosk_Lines");

            migrationBuilder.DropTable(
                name: "Ecare_Kiosk_Clients");
        }
    }
}
