using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AudioProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioSummaryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "audio_records",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

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
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "summary",
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
        }
    }
}
