using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerPreferredArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredAreaCode",
                table: "CareerGoalVersions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredAreaLevel",
                table: "CareerGoalVersions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredAreaTitle",
                table: "CareerGoalVersions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredAreaCode",
                table: "CareerGoalVersions");

            migrationBuilder.DropColumn(
                name: "PreferredAreaLevel",
                table: "CareerGoalVersions");

            migrationBuilder.DropColumn(
                name: "PreferredAreaTitle",
                table: "CareerGoalVersions");
        }
    }
}
