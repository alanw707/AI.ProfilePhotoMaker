using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    public partial class UnifyCredits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE UserProfiles SET Credits = Credits + PurchasedCredits");

            migrationBuilder.DropColumn(
                name: "PurchasedCredits",
                table: "UserProfiles");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PurchasedCredits",
                table: "UserProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
