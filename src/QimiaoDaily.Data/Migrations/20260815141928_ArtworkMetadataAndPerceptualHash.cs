using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QimiaoDaily.Data.Migrations
{
    /// <inheritdoc />
    public partial class ArtworkMetadataAndPerceptualHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PerceptualHash",
                table: "seen_artworks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CharacterName",
                table: "artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FranchiseName",
                table: "artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "artworks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerceptualHash",
                table: "artworks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMetadata",
                table: "artworks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "artworks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_seen_artworks_PerceptualHash",
                table: "seen_artworks",
                column: "PerceptualHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_seen_artworks_PerceptualHash",
                table: "seen_artworks");

            migrationBuilder.DropColumn(
                name: "PerceptualHash",
                table: "seen_artworks");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "artworks");

            migrationBuilder.DropColumn(
                name: "CharacterName",
                table: "artworks");

            migrationBuilder.DropColumn(
                name: "FranchiseName",
                table: "artworks");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "artworks");

            migrationBuilder.DropColumn(
                name: "PerceptualHash",
                table: "artworks");

            migrationBuilder.DropColumn(
                name: "SourceMetadata",
                table: "artworks");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "artworks");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "artworks");
        }
    }
}
