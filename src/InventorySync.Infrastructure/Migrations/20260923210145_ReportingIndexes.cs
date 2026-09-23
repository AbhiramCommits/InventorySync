using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SyncAuditEntries_Action_TimestampUtc",
                table: "SyncAuditEntries",
                columns: new[] { "Action", "TimestampUtc" })
                .Annotation("SqlServer:Include", new[] { "SyncRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OpenByStatus",
                table: "PurchaseOrders",
                column: "Status",
                filter: "Status IN (1, 2)")
                .Annotation("SqlServer:Include", new[] { "Id", "PoNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_Sku_Open",
                table: "PurchaseOrderLines",
                column: "Sku")
                .Annotation("SqlServer:Include", new[] { "QuantityOrdered", "QuantityReceived" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_LowStock",
                table: "InventoryItems",
                columns: new[] { "WarehouseCode", "QuantityOnHand" },
                filter: "QuantityOnHand < 25");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_Whse_Valuation",
                table: "InventoryItems",
                column: "WarehouseCode")
                .Annotation("SqlServer:Include", new[] { "QuantityOnHand", "UnitCost" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncAuditEntries_Action_TimestampUtc",
                table: "SyncAuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_OpenByStatus",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_Sku_Open",
                table: "PurchaseOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_LowStock",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_Whse_Valuation",
                table: "InventoryItems");
        }
    }
}
