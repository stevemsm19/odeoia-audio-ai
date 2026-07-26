using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AudioProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompressionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "compressed_storage_url",
                table: "audio_records",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "compression_duration_ms",
                table: "audio_records",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "audio_records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "compressed_storage_url",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "compression_duration_ms",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "status",
                table: "audio_records");
        }
    }
}
