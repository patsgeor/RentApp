using System.Globalization;
using API.Data.Contexts;
using API.DTOs.Dashboard;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Services;

public class DashboardService(AppDbContext context) : IDashboardService
{
    public async Task<DashboardDto> GetAsync()
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sixMonthsAgo = startOfMonth.AddMonths(-5);

        var activeContracts = await context.Contracts
            .CountAsync(c => c.Status == RentalStatus.Active);

        // Τα ακυρωμένα συμβόλαια εξαιρούνται από κάθε στατιστικό: παραμένουν στη
        // βάση για λόγους ιχνηλασιμότητας, αλλά δεν αντιπροσωπεύουν πραγματική
        // επιχειρηματική δραστηριότητα.
        var newContractsThisMonth = await context.Contracts
            .CountAsync(c => c.CreatedAt >= startOfMonth && c.Status != RentalStatus.Cancelled);

        var monthlyIncome = await context.Payments
            .Where(p => p.TransactionType == TransactionType.Income && p.PaymentDate >= startOfMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var monthlyExpenses = await context.Payments
            .Where(p => p.TransactionType == TransactionType.Expense && p.PaymentDate >= startOfMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var availableAssets = await context.Assets
            .CountAsync(a => a.Status == AssetStatus.Active);

        var rentedAssets = await context.ContractAssets
            .CountAsync(ca =>
                ca.Contract.Status == RentalStatus.Active &&
                ca.StartDate <= now && ca.EndDate >= now);

        var totalAssets = await context.Assets.CountAsync();


        // Υπολογίζεται από τις δόσεις (όχι από την κατάσταση του συμβολαίου) ώστε να
        // συμφωνεί με την οθόνη «Οφειλές» στην οποία οδηγεί το ίδιο KPI. Με φίλτρο
        // κατάστασης συμβολαίου, ανεξόφλητες δόσεις ληγμένων συμβολαίων χάνονταν.
        var outstandingBalance = await context.Installments
            .Where(i => i.Status != InstallmentStatus.Paid
                     && i.Status != InstallmentStatus.Cancelled
                     && i.Contract.Status != RentalStatus.Cancelled)
            .Select(i => i.TotalAmount - i.AllocatedAmount)
            .SumAsync(o => (decimal?)o) ?? 0m;

        // Ληξιπρόθεσμα = συμβόλαια με απλήρωτη δόση που πέρασε η ημ. λήξης της.
        // Δεν φιλτράρουμε σε κατάσταση συμβολαίου: ένα ληγμένο (Ολοκληρωμένο)
        // συμβόλαιο μπορεί κάλλιστα να έχει ακόμα ανεξόφλητο υπόλοιπο.
        var overdueRaw = await context.Contracts
            .Where(c => c.Status != RentalStatus.Cancelled && c.Installments.Any(i =>
                i.Status != InstallmentStatus.Paid &&
                i.Status != InstallmentStatus.Cancelled &&
                i.DueDate < now))
            .Select(c => new
            {
                c.Id,
                c.EndDate,
                CustomerName = c.Customer.Name,
                Outstanding = c.Installments
                    .Where(i => i.Status != InstallmentStatus.Paid && i.Status != InstallmentStatus.Cancelled)
                    .Sum(i => (decimal?)(i.TotalAmount - i.AllocatedAmount)) ?? 0m,
                AssetNames = c.ContractAssets.Select(ca => ca.Asset.Name).ToList()
            })
            .OrderBy(c => c.EndDate)
            .Take(15)
            .ToListAsync();

        var overdueContracts = overdueRaw
            .Where(x => x.Outstanding > 0)
            .Take(10)
            .Select(x => new OverdueContractDto
            {
                Id = x.Id,
                EndDate = x.EndDate,
                CustomerName = x.CustomerName,
                OutstandingBalance = x.Outstanding,
                AssetNames = x.AssetNames
            }).ToList();

        // Recent transactions
        var recentTransactions = await context.Payments
            .OrderByDescending(p => p.PaymentDate)
            .Take(8)
            .Select(p => new RecentTransactionDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                TransactionType = p.TransactionType,
                PaymentMethod = p.PaymentMethod,
                CustomerName = p.Allocations.Select(a => a.Installment.Contract.Customer.Name).FirstOrDefault(),
                Label = p.Description ?? p.Notes ?? p.Allocations.Select(a => a.Installment.Contract.Customer.Name).FirstOrDefault()
            })
            .ToListAsync();

        // Monthly chart — fetch payments in window, group in memory
        var chartPayments = await context.Payments
            .Where(p => p.PaymentDate >= sixMonthsAgo)
            .Select(p => new { p.PaymentDate, p.Amount, p.TransactionType })
            .ToListAsync();

        var el = new CultureInfo("el-GR");
        var monthlyChart = Enumerable.Range(0, 6)
            .Select(i => startOfMonth.AddMonths(-5 + i))
            .Select(ms =>
            {
                var me = ms.AddMonths(1);
                var slice = chartPayments.Where(p => p.PaymentDate >= ms && p.PaymentDate < me).ToList();
                return new MonthlyChartDto
                {
                    Month = ms.ToString("MMM yy", el),
                    Income   = slice.Where(p => p.TransactionType == TransactionType.Income).Sum(p => p.Amount),
                    Expenses = slice.Where(p => p.TransactionType == TransactionType.Expense).Sum(p => p.Amount)
                };
            }).ToList();

        // ── Τζίρος τρέχοντος έτους ─────────────────────────────────
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearlyIncome = await context.Payments
            .Where(p => p.TransactionType == TransactionType.Income && p.PaymentDate >= startOfYear)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var yearlyExpenses = await context.Payments
            .Where(p => p.TransactionType == TransactionType.Expense && p.PaymentDate >= startOfYear)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        // ── Ετήσιο chart (12 μήνες τρέχοντος έτους) ────────────────
        var yearlyPayments = await context.Payments
            .Where(p => p.PaymentDate >= startOfYear)
            .Select(p => new { p.PaymentDate, p.Amount, p.TransactionType })
            .ToListAsync();

        var yearlyChart = Enumerable.Range(0, 12)
            .Select(i => new DateTime(now.Year, i + 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .Select(ms =>
            {
                var me = ms.AddMonths(1);
                var slice = yearlyPayments.Where(p => p.PaymentDate >= ms && p.PaymentDate < me).ToList();
                return new MonthlyChartDto
                {
                    Month    = ms.ToString("MMM", el),
                    Income   = slice.Where(p => p.TransactionType == TransactionType.Income).Sum(p => p.Amount),
                    Expenses = slice.Where(p => p.TransactionType == TransactionType.Expense).Sum(p => p.Amount)
                };
            }).ToList();

        // ── Top 5 Πάγια (βάσει εισπράξεων) ────────────────────────
        var assetRevRaw = await context.ContractAssets
            .Where(ca => ca.Contract.Status != RentalStatus.Cancelled)
            .Select(ca => new
            {
                ca.AssetId,
                AssetName = ca.Asset.Name,
                 Paid = ca.Contract.Installments.Sum(i => (decimal?)i.AllocatedAmount) ?? 0m
            })
            .ToListAsync();

        var topAssets = assetRevRaw
            .GroupBy(x => new { x.AssetId, x.AssetName })
            .Select(g => new TopAssetDto
            {
                Id            = g.Key.AssetId,
                Name          = g.Key.AssetName,
                TotalRevenue  = g.Sum(x => x.Paid),
                ContractCount = g.Count()
            })
            .OrderByDescending(a => a.TotalRevenue)
            .Take(5)
            .ToList();

        // ── Top 5 Πελάτες με υψηλό υπόλοιπο ───────────────────────
        var topCustomers = await context.Customers
            .Select(c => new TopCustomerDto
            {
                Id = c.Id,
                Name = c.Name,
                OutstandingBalance = c.Contracts
                    .Where(ct => ct.Status != RentalStatus.Cancelled)
                    .SelectMany(ct => ct.Installments)
                    .Where(i => i.Status != InstallmentStatus.Paid && i.Status != InstallmentStatus.Cancelled)
                    .Sum(i => (decimal?)(i.TotalAmount - i.AllocatedAmount)) ?? 0m,
                ActiveContracts = c.Contracts.Count(ct => ct.Status == RentalStatus.Active)
            })
            .Where(c => c.OutstandingBalance > 0)
            .OrderByDescending(c => c.OutstandingBalance)
            .Take(5)
            .ToListAsync();

        // ── Πρόβλεψη εισπράξεων επόμενων 3 μηνών (από δόσεις) ─────
        // ΣΗΜΕΙΩΣΗ: αν το DbSet σου λέγεται διαφορετικά, άλλαξε το context.Installments
        var next3Months = startOfMonth.AddMonths(3);
        // Μόνο ό,τι απομένει να εισπραχθεί (χωρίς εξοφλημένες/ακυρωμένες) και επί
        // του συνολικού ποσού με φόρο, όπως εμφανίζεται και στις «Οφειλές».
        var upcomingRaw = await context.Installments
            .Where(i => i.Contract.Status != RentalStatus.Cancelled &&
                        i.Status != InstallmentStatus.Paid &&
                        i.Status != InstallmentStatus.Cancelled &&
                        i.DueDate >= now && i.DueDate < next3Months)
            .Select(i => new { i.DueDate, Remaining = i.TotalAmount - i.AllocatedAmount })
            .ToListAsync();

        var upcomingInstallments = Enumerable.Range(0, 3)
            .Select(i => startOfMonth.AddMonths(i))
            .Select(ms =>
            {
                var me = ms.AddMonths(1);
                var slice = upcomingRaw.Where(i => i.DueDate >= ms && i.DueDate < me).ToList();
                return new UpcomingInstallmentDto
                {
                    Month          = ms.ToString("MMMM yyyy", el),
                    ExpectedAmount = slice.Sum(i => i.Remaining),
                    Count          = slice.Count
                };
            }).ToList();

        return new DashboardDto
        {
            Kpi = new KpiDto
            {
                ActiveContracts         = activeContracts,
                NewContractsThisMonth   = newContractsThisMonth,
                MonthlyIncome           = monthlyIncome,
                YearlyIncome            = yearlyIncome,
                MonthlyExpenses         = monthlyExpenses,
                YearlyExpenses          = yearlyExpenses,
                TotalOutstandingBalance = outstandingBalance,
                AvailableAssets         = availableAssets,
                RentedAssets            = rentedAssets,
                TotalAssets             = totalAssets
            },
            OverdueContracts      = overdueContracts,
            RecentTransactions    = recentTransactions,
            MonthlyChart          = monthlyChart,
            YearlyChart           = yearlyChart,           
            TopAssets             = topAssets,            
            TopCustomers          = topCustomers,          
            UpcomingInstallments  = upcomingInstallments,  
        };
    }
}
