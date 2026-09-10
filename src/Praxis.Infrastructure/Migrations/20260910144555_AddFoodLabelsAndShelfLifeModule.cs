using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Praxis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodLabelsAndShelfLifeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabelTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TemplateType = table.Column<string>(type: "TEXT", nullable: false),
                    WidthMm = table.Column<decimal>(type: "TEXT", nullable: false),
                    HeightMm = table.Column<decimal>(type: "TEXT", nullable: false),
                    IncludeQrCode = table.Column<bool>(type: "INTEGER", nullable: false),
                    IncludeLogo = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabelTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchCode = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalBatchCode = table.Column<string>(type: "TEXT", nullable: true),
                    ManufacturingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginalExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBatches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ValidityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ProductCategory = table.Column<string>(type: "TEXT", nullable: true),
                    LabelType = table.Column<int>(type: "INTEGER", nullable: true),
                    OperationType = table.Column<int>(type: "INTEGER", nullable: true),
                    StorageCondition = table.Column<int>(type: "INTEGER", nullable: true),
                    MaximumTemperature = table.Column<decimal>(type: "TEXT", nullable: true),
                    ValidityValue = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidityUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowManualExpiration = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresTechnicalBasis = table.Column<bool>(type: "INTEGER", nullable: false),
                    TechnicalBasis = table.Column<string>(type: "TEXT", nullable: true),
                    RegulatoryReference = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidityRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidityRules_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ValidityRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValidityRules_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FoodLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ValidityRuleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LabelType = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    InternalBatchCode = table.Column<string>(type: "TEXT", nullable: false),
                    ManufacturedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PortionedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValidityStartAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CalculatedExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ManualExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValiditySource = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidityJustification = table.Column<string>(type: "TEXT", nullable: true),
                    StorageCondition = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageTemperatureMin = table.Column<decimal>(type: "TEXT", nullable: true),
                    StorageTemperatureMax = table.Column<decimal>(type: "TEXT", nullable: true),
                    StorageInstructions = table.Column<string>(type: "TEXT", nullable: true),
                    PublicToken = table.Column<string>(type: "TEXT", nullable: false),
                    PrintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", nullable: true),
                    DiscardedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DiscardedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DiscardReason = table.Column<string>(type: "TEXT", nullable: true),
                    DiscardQuantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    DiscardUnit = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodLabels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoodLabels_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FoodLabels_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoodLabels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoodLabels_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoodLabels_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FoodLabels_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoodLabels_Users_DiscardedByUserId",
                        column: x => x.DiscardedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FoodLabels_ValidityRules_ValidityRuleId",
                        column: x => x.ValidityRuleId,
                        principalTable: "ValidityRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FoodLabelAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LabelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodLabelAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoodLabelAudits_FoodLabels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "FoodLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FoodLabelAudits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FoodLabelAudits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LabelPrints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LabelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrintedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrintedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PrinterName = table.Column<string>(type: "TEXT", nullable: true),
                    TemplateUsed = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelPrints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabelPrints_FoodLabels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "FoodLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabelPrints_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabelPrints_Users_PrintedByUserId",
                        column: x => x.PrintedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabelAudits_LabelId",
                table: "FoodLabelAudits",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabelAudits_TenantId_LabelId",
                table: "FoodLabelAudits",
                columns: new[] { "TenantId", "LabelId" });

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabelAudits_UserId",
                table: "FoodLabelAudits",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_CancelledByUserId",
                table: "FoodLabels",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_CreatedByUserId",
                table: "FoodLabels",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_DiscardedByUserId",
                table: "FoodLabels",
                column: "DiscardedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_ProductBatchId",
                table: "FoodLabels",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_ProductId",
                table: "FoodLabels",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_PublicToken",
                table: "FoodLabels",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_TenantId_CalculatedExpirationDate",
                table: "FoodLabels",
                columns: new[] { "TenantId", "CalculatedExpirationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_TenantId_InternalBatchCode",
                table: "FoodLabels",
                columns: new[] { "TenantId", "InternalBatchCode" });

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_TenantId_ProductId",
                table: "FoodLabels",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_TenantId_UnitId",
                table: "FoodLabels",
                columns: new[] { "TenantId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_UnitId",
                table: "FoodLabels",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLabels_ValidityRuleId",
                table: "FoodLabels",
                column: "ValidityRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_LabelPrints_LabelId",
                table: "LabelPrints",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_LabelPrints_PrintedByUserId",
                table: "LabelPrints",
                column: "PrintedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LabelPrints_TenantId_LabelId",
                table: "LabelPrints",
                columns: new[] { "TenantId", "LabelId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabelTemplates_TenantId_TemplateType",
                table: "LabelTemplates",
                columns: new[] { "TenantId", "TemplateType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductId",
                table: "ProductBatches",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_TenantId_BatchCode",
                table: "ProductBatches",
                columns: new[] { "TenantId", "BatchCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_TenantId_ProductId",
                table: "ProductBatches",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Category",
                table: "Products",
                columns: new[] { "TenantId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_UnitId",
                table: "Products",
                columns: new[] { "TenantId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_UnitId",
                table: "Products",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidityRules_ProductId",
                table: "ValidityRules",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidityRules_TenantId_IsActive_UnitId_ProductId",
                table: "ValidityRules",
                columns: new[] { "TenantId", "IsActive", "UnitId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ValidityRules_UnitId",
                table: "ValidityRules",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FoodLabelAudits");

            migrationBuilder.DropTable(
                name: "LabelPrints");

            migrationBuilder.DropTable(
                name: "LabelTemplates");

            migrationBuilder.DropTable(
                name: "FoodLabels");

            migrationBuilder.DropTable(
                name: "ProductBatches");

            migrationBuilder.DropTable(
                name: "ValidityRules");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
