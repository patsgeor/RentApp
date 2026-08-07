using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StackTrace",
                table: "ErrorLogs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_payment_tenant_type_date",
                table: "Payments",
                columns: new[] { "TenantId", "TransactionType", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "idx_installment_tenant_status_due",
                table: "Installments",
                columns: new[] { "TenantId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_Timestamp",
                table: "ErrorLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "idx_contract_tenant_status_end",
                table: "Contracts",
                columns: new[] { "TenantId", "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "TenantId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_payment_tenant_type_date",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "idx_installment_tenant_status_due",
                table: "Installments");

            migrationBuilder.DropIndex(
                name: "IX_ErrorLogs_Timestamp",
                table: "ErrorLogs");

            migrationBuilder.DropIndex(
                name: "idx_contract_tenant_status_end",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "StackTrace",
                table: "ErrorLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
