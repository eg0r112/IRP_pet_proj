using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace irp_pet.Migrations
{
    /// <inheritdoc />
    public partial class ReseedDemoIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Удаляем старые однотипные инциденты — при следующем старте DbSeeder зальёт разнообразные демо.
            migrationBuilder.Sql(
                """
                DELETE FROM notification_attempts;
                DELETE FROM incident_timeline;
                DELETE FROM alerts;
                DELETE FROM incidents;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
