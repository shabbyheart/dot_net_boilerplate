using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerplate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_administrator",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_name",
                table: "tenants",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_roles_one_administrator_per_tenant",
                table: "roles",
                column: "tenant_id",
                unique: true,
                filter: "is_administrator");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_name",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ux_roles_one_administrator_per_tenant",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "is_administrator",
                table: "roles");
        }
    }
}
