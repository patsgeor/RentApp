using static API.Entities.Enums;

namespace API.DTOs.Dashboard;


public class OverdueContractDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = null!;
    public decimal OutstandingBalance { get; set; }
    public DateTime EndDate { get; set; }
    public List<string> AssetNames { get; set; } = [];
}

public class RecentTransactionDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public TransactionType TransactionType { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? CustomerName { get; set; }
    public string? Label { get; set; }
}

public class MonthlyChartDto
{
    public string Month { get; set; } = null!;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
}



public class DashboardDto
{
    public KpiDto Kpi { get; set; } = new();
    public List<OverdueContractDto> OverdueContracts { get; set; } = [];
    public List<RecentTransactionDto> RecentTransactions { get; set; } = [];
    public List<MonthlyChartDto> MonthlyChart { get; set; } = [];
    // ↓ ΝΕΑ
    public List<MonthlyChartDto> YearlyChart { get; set; } = [];
    public List<TopAssetDto> TopAssets { get; set; } = [];
    public List<TopCustomerDto> TopCustomers { get; set; } = [];
    public List<UpcomingInstallmentDto> UpcomingInstallments { get; set; } = [];
}

public class KpiDto
{
    public int ActiveContracts { get; set; }
    public int NewContractsThisMonth { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal TotalOutstandingBalance { get; set; }
    public int AvailableAssets { get; set; }
    public int RentedAssets { get; set; }
    public int TotalAssets { get; set; }
    // ↓ ΝΕΟ
    public decimal YearlyIncome { get; set; }
}


// ↓ ΝΕΑ
public class TopAssetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal TotalRevenue { get; set; }
    public int ContractCount { get; set; }
}

public class TopCustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal OutstandingBalance { get; set; }
    public int ActiveContracts { get; set; }
}

public class UpcomingInstallmentDto
{
    public string Month { get; set; } = null!;
    public decimal ExpectedAmount { get; set; }
    public int Count { get; set; }
}