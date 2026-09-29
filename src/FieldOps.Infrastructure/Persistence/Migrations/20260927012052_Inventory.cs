using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reorder_level = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parts", x => x.id);
                    table.CheckConstraint("ck_parts_unit_cost", "unit_cost >= 0");
                    table.CheckConstraint("ck_parts_unit_price", "unit_price >= 0");
                });

            migrationBuilder.CreateTable(
                name: "stock_levels",
                columns: table => new
                {
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_levels", x => new { x.part_id, x.stock_location_id });
                    table.CheckConstraint("ck_stock_levels_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_stock_levels_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_levels_stock_locations_stock_location_id",
                        column: x => x.stock_location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    from_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.CheckConstraint("ck_stock_movements_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_stock_movements_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_stock_locations_from_location_id",
                        column: x => x.from_location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_stock_locations_to_location_id",
                        column: x => x.to_location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_order_parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    stock_movement_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_parts", x => x.id);
                    table.CheckConstraint("ck_work_order_parts_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_work_order_parts_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_order_parts_stock_locations_stock_location_id",
                        column: x => x.stock_location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_order_parts_stock_movements_stock_movement_id",
                        column: x => x.stock_movement_id,
                        principalTable: "stock_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_order_parts_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "stock_locations",
                columns: new[] { "id", "created_at", "created_by", "is_active", "name", "technician_id", "type", "updated_at", "updated_by" },
                values: new object[] { new Guid("01a0e000-0000-7000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Main warehouse", null, "Warehouse", null, null });

            migrationBuilder.CreateIndex(
                name: "ix_parts_sku",
                table: "parts",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_levels_stock_location_id",
                table: "stock_levels",
                column: "stock_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_created_by",
                table: "stock_movements",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_from_location_id",
                table: "stock_movements",
                column: "from_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_part_id_created_at",
                table: "stock_movements",
                columns: new[] { "part_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_to_location_id",
                table: "stock_movements",
                column: "to_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_work_order_id",
                table: "stock_movements",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_parts_part_id",
                table: "work_order_parts",
                column: "part_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_parts_stock_location_id",
                table: "work_order_parts",
                column: "stock_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_parts_stock_movement_id",
                table: "work_order_parts",
                column: "stock_movement_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_parts_work_order_id",
                table: "work_order_parts",
                column: "work_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_levels");

            migrationBuilder.DropTable(
                name: "work_order_parts");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "parts");

            migrationBuilder.DeleteData(
                table: "stock_locations",
                keyColumn: "id",
                keyValue: new Guid("01a0e000-0000-7000-8000-000000000001"));
        }
    }
}
