using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId_ClientId_RevokedAt",
                table: "user_sessions",
                columns: new[] { "UserId", "ClientId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId_LastSeenAt",
                table: "user_sessions",
                columns: new[] { "UserId", "LastSeenAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId_RevokedAt",
                table: "user_sessions",
                columns: new[] { "UserId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_login_attempts_Identifier_CreatedAt",
                table: "login_attempts",
                columns: new[] { "Identifier", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_login_attempts_IpAddress_CreatedAt",
                table: "login_attempts",
                columns: new[] { "IpAddress", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_EventType_CreatedAt",
                table: "audit_events",
                columns: new[] { "EventType", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_UserId_CreatedAt",
                table: "audit_events",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_sessions_UserId_ClientId_RevokedAt",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_user_sessions_UserId_LastSeenAt",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_user_sessions_UserId_RevokedAt",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_login_attempts_Identifier_CreatedAt",
                table: "login_attempts");

            migrationBuilder.DropIndex(
                name: "IX_login_attempts_IpAddress_CreatedAt",
                table: "login_attempts");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_EventType_CreatedAt",
                table: "audit_events");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_UserId_CreatedAt",
                table: "audit_events");
        }
    }
}
