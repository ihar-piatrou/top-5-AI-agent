using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Top5Agent.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHeyGenTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "heygen_audio_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ScriptSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoiceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScriptText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "completed"),
                    AudioUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LocalPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heygen_audio_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_heygen_audio_files_script_sections_ScriptSectionId",
                        column: x => x.ScriptSectionId,
                        principalTable: "script_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "heygen_avatar_videos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ScriptSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeygenVideoId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AvatarId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VoiceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScriptText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    VideoUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LocalPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heygen_avatar_videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_heygen_avatar_videos_script_sections_ScriptSectionId",
                        column: x => x.ScriptSectionId,
                        principalTable: "script_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_heygen_audio_files_ScriptSectionId",
                table: "heygen_audio_files",
                column: "ScriptSectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_heygen_avatar_videos_ScriptSectionId",
                table: "heygen_avatar_videos",
                column: "ScriptSectionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "heygen_audio_files");

            migrationBuilder.DropTable(
                name: "heygen_avatar_videos");
        }
    }
}
