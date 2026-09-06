using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AudioProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNoiseReductionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "filtered_storage_url",
                table: "audio_records",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "noise_reduction_duration_ms",
                table: "audio_records",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "noise_reduction_error",
                table: "audio_records",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "noise_reduction_status",
                table: "audio_records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "filtered_storage_url",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "noise_reduction_duration_ms",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "noise_reduction_error",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "noise_reduction_status",
                table: "audio_records");
        }
    }
}
