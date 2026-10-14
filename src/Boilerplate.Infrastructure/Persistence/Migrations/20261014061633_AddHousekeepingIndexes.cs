using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerplate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHousekeepingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_one_time_tokens_expires_at",
                table: "one_time_tokens",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_expires_at",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_processed",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_one_time_tokens_expires_at",
                table: "one_time_tokens");
        }
    }
}
