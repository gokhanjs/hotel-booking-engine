using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_properties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "room_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    count_of_rooms = table.Column<int>(type: "integer", nullable: false),
                    max_adults = table.Column<int>(type: "integer", nullable: false),
                    max_children = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_types", x => x.id);
                    table.CheckConstraint("ck_room_types_count_of_rooms", "count_of_rooms >= 1");
                    table.CheckConstraint("ck_room_types_max_adults", "max_adults >= 1");
                    table.CheckConstraint("ck_room_types_max_children", "max_children >= 0");
                    table.ForeignKey(
                        name: "fk_room_types_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rate_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sell_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    meal_plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    child_fee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    parent_rate_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    derived_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    derived_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    occupancies = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_plans", x => x.id);
                    table.CheckConstraint("ck_rate_plans_child_fee", "child_fee >= 0");
                    table.CheckConstraint("ck_rate_plans_derived_complete", "(parent_rate_plan_id IS NULL) = (derived_type IS NULL) AND (derived_type IS NULL) = (derived_value IS NULL)");
                    table.ForeignKey(
                        name: "fk_rate_plans_rate_plans_parent_rate_plan_id",
                        column: x => x.parent_rate_plan_id,
                        principalTable: "rate_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rate_plans_room_types_room_type_id",
                        column: x => x.room_type_id,
                        principalTable: "room_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_type_availability",
                columns: table => new
                {
                    room_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    availability = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_type_availability", x => new { x.room_type_id, x.date });
                    table.CheckConstraint("ck_room_type_availability_availability", "availability >= 0");
                    table.ForeignKey(
                        name: "fk_room_type_availability_room_types_room_type_id",
                        column: x => x.room_type_id,
                        principalTable: "room_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rate_plan_restrictions",
                columns: table => new
                {
                    rate_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    stop_sell = table.Column<bool>(type: "boolean", nullable: false),
                    closed_to_arrival = table.Column<bool>(type: "boolean", nullable: false),
                    closed_to_departure = table.Column<bool>(type: "boolean", nullable: false),
                    min_stay_arrival = table.Column<int>(type: "integer", nullable: false),
                    min_stay_through = table.Column<int>(type: "integer", nullable: false),
                    max_stay = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_plan_restrictions", x => new { x.rate_plan_id, x.date });
                    table.CheckConstraint("ck_rate_plan_restrictions_max_stay", "max_stay IS NULL OR max_stay >= 1");
                    table.CheckConstraint("ck_rate_plan_restrictions_min_stay_arrival", "min_stay_arrival >= 1");
                    table.CheckConstraint("ck_rate_plan_restrictions_min_stay_through", "min_stay_through >= 1");
                    table.CheckConstraint("ck_rate_plan_restrictions_rate", "rate IS NULL OR rate >= 0");
                    table.ForeignKey(
                        name: "fk_rate_plan_restrictions_rate_plans_rate_plan_id",
                        column: x => x.rate_plan_id,
                        principalTable: "rate_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rate_plans_parent_rate_plan_id",
                table: "rate_plans",
                column: "parent_rate_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_rate_plans_room_type_id_code",
                table: "rate_plans",
                columns: new[] { "room_type_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_types_property_id_code",
                table: "room_types",
                columns: new[] { "property_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rate_plan_restrictions");

            migrationBuilder.DropTable(
                name: "room_type_availability");

            migrationBuilder.DropTable(
                name: "rate_plans");

            migrationBuilder.DropTable(
                name: "room_types");

            migrationBuilder.DropTable(
                name: "properties");
        }
    }
}
