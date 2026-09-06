using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Praxis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActionPlanAndEvidences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActionItems_Users_ResponsibleUserId",
                table: "ActionItems");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "How",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HowMuch",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ActionItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleName",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ActionItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatedAt",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidatedByUserId",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationComment",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "What",
                table: "ActionItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Where",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Why",
                table: "ActionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActionPlanEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ObjectKey = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionPlanEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionPlanEvidences_ActionItems_ActionPlanId",
                        column: x => x.ActionPlanId,
                        principalTable: "ActionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionPlanEvidences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActionPlanEvidences_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_DueDate",
                table: "ActionItems",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_Priority",
                table: "ActionItems",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_Status",
                table: "ActionItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_TenantId",
                table: "ActionItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_ValidatedByUserId",
                table: "ActionItems",
                column: "ValidatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionPlanEvidences_ActionPlanId",
                table: "ActionPlanEvidences",
                column: "ActionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionPlanEvidences_ObjectKey",
                table: "ActionPlanEvidences",
                column: "ObjectKey");

            migrationBuilder.CreateIndex(
                name: "IX_ActionPlanEvidences_TenantId",
                table: "ActionPlanEvidences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionPlanEvidences_UploadedByUserId",
                table: "ActionPlanEvidences",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItems_Tenants_TenantId",
                table: "ActionItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItems_Users_ResponsibleUserId",
                table: "ActionItems",
                column: "ResponsibleUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItems_Users_ValidatedByUserId",
                table: "ActionItems",
                column: "ValidatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActionItems_Tenants_TenantId",
                table: "ActionItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ActionItems_Users_ResponsibleUserId",
                table: "ActionItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ActionItems_Users_ValidatedByUserId",
                table: "ActionItems");

            migrationBuilder.DropTable(
                name: "ActionPlanEvidences");

            migrationBuilder.DropIndex(
                name: "IX_ActionItems_DueDate",
                table: "ActionItems");

            migrationBuilder.DropIndex(
                name: "IX_ActionItems_Priority",
                table: "ActionItems");

            migrationBuilder.DropIndex(
                name: "IX_ActionItems_Status",
                table: "ActionItems");

            migrationBuilder.DropIndex(
                name: "IX_ActionItems_TenantId",
                table: "ActionItems");

            migrationBuilder.DropIndex(
                name: "IX_ActionItems_ValidatedByUserId",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "How",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "HowMuch",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "ResponsibleName",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "ValidatedByUserId",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "ValidationComment",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "What",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "Where",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "Why",
                table: "ActionItems");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItems_Users_ResponsibleUserId",
                table: "ActionItems",
                column: "ResponsibleUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
