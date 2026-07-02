using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBaseEntityFromContractAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractAssets_Members_CreatedBy",
                table: "ContractAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractAssets_Members_DeletedBy",
                table: "ContractAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractAssets_Members_UpdatedBy",
                table: "ContractAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractAssets_Tenants_TenantId",
                table: "ContractAssets");

            migrationBuilder.DropIndex(
                name: "IX_ContractAssets_CreatedBy",
                table: "ContractAssets");

            migrationBuilder.DropIndex(
                name: "IX_ContractAssets_DeletedBy",
                table: "ContractAssets");

            migrationBuilder.DropIndex(
                name: "IX_ContractAssets_TenantId",
                table: "ContractAssets");

            migrationBuilder.DropIndex(
                name: "IX_ContractAssets_UpdatedBy",
                table: "ContractAssets");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ContractAssets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ContractAssets");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ContractAssets");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ContractAssets");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ContractAssets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ContractAssets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ContractAssets");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ContractAssets");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ContractAssets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ContractAssets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ContractAssets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ContractAssets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ContractAssets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ContractAssets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ContractAssets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ContractAssets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ContractAssets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ContractAssets",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAssets_CreatedBy",
                table: "ContractAssets",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAssets_DeletedBy",
                table: "ContractAssets",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAssets_TenantId",
                table: "ContractAssets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAssets_UpdatedBy",
                table: "ContractAssets",
                column: "UpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAssets_Members_CreatedBy",
                table: "ContractAssets",
                column: "CreatedBy",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAssets_Members_DeletedBy",
                table: "ContractAssets",
                column: "DeletedBy",
                principalTable: "Members",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAssets_Members_UpdatedBy",
                table: "ContractAssets",
                column: "UpdatedBy",
                principalTable: "Members",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAssets_Tenants_TenantId",
                table: "ContractAssets",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
