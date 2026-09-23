using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LocalModificationTrackingAndRetryRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentSyncRunId",
                table: "SyncRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocallyModifiedUtc",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocallyModifiedUtc",
                table: "InventoryItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_ParentSyncRunId",
                table: "SyncRuns",
                column: "ParentSyncRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_SyncRuns_SyncRuns_ParentSyncRunId",
                table: "SyncRuns",
                column: "ParentSyncRunId",
                principalTable: "SyncRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SyncRuns_SyncRuns_ParentSyncRunId",
                table: "SyncRuns");

            migrationBuilder.DropIndex(
                name: "IX_SyncRuns_ParentSyncRunId",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "ParentSyncRunId",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "LocallyModifiedUtc",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "LocallyModifiedUtc",
                table: "InventoryItems");
        }
    }
}
