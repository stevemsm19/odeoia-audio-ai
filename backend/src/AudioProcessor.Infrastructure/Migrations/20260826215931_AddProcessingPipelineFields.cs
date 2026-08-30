using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AudioProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingPipelineFields : Migration
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

            migrationBuilder.AddColumn<long>(
                name: "summary_duration_ms",
                table: "audio_records",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary_error",
                table: "audio_records",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary_status",
                table: "audio_records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "summary_storage_url",
                table: "audio_records",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "transcription_duration_ms",
                table: "audio_records",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transcription_error",
                table: "audio_records",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transcription_status",
                table: "audio_records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "user_email",
                table: "audio_records",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);
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

            migrationBuilder.DropColumn(
                name: "summary_duration_ms",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "summary_error",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "summary_status",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "summary_storage_url",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "transcription_duration_ms",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "transcription_error",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "transcription_status",
                table: "audio_records");

            migrationBuilder.DropColumn(
                name: "user_email",
                table: "audio_records");
        }
    }
}
