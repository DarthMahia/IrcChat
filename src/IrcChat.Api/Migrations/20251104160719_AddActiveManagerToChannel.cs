using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations;

/// <inheritdoc />
public partial class AddActiveManagerToChannel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ActiveManager",
            table: "Channels",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ActiveManager",
            table: "Channels");
    }
}