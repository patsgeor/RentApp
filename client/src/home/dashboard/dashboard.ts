import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe, NgClass } from '@angular/common';
import { DashboardService } from '../../core/services/dashboard-service';
import {
  DashboardDto, MonthlyChartDto, OverdueContractDto,
  RecentTransactionDto, TopAssetDto, TopCustomerDto, UpcomingInstallmentDto
} from '../../types/dashboard';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, CurrencyPipe, DatePipe, NgClass],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private svc = inject(DashboardService);

  data    = signal<DashboardDto | null>(null);
  loading = signal(true);
  error   = signal<string | null>(null);
  chartMode = signal<'monthly' | 'yearly'>('monthly');  
  currentYear = new Date().getFullYear();
  

  kpi                  = computed(() => this.data()?.kpi);
  overdue              = computed(() => this.data()?.overdueContracts ?? []);
  transactions         = computed(() => this.data()?.recentTransactions ?? []);
  topAssets            = computed(() => this.data()?.topAssets ?? []);
  topCustomers         = computed(() => this.data()?.topCustomers ?? []);
  upcomingInstallments = computed(() => this.data()?.upcomingInstallments ?? []);

  // Chart με toggle μήνας/έτος
  chart = computed(() =>
    this.chartMode() === 'monthly'
      ? (this.data()?.monthlyChart ?? [])
      : (this.data()?.yearlyChart ?? [])
  );

  chartMax = computed(() => {
    const max = Math.max(...this.chart().map(b => Math.max(b.income, b.expenses)), 1);
    return max;
  });

  barHeight(value: number): number {
    const max = this.chartMax();
    return max > 0 ? Math.round((value / max) * 100) : 0;
  }

  // Forecast bar για πρόβλεψη
  forecastMax = computed(() =>
    Math.max(...this.upcomingInstallments().map(i => i.expectedAmount), 1)
  );
  forecastBar(value: number): number {
    const max = this.forecastMax();
    return max > 0 ? Math.round((value / max) * 100) : 0;
  }

  ngOnInit() {
    this.svc.get().subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: () => { this.error.set('Αδυναμία φόρτωσης δεδομένων.'); this.loading.set(false); }
    });
  }

  daysSince(dateStr: string): number {
    return Math.floor((Date.now() - new Date(dateStr).getTime()) / 86_400_000);
  }

  methodLabel(m: number): string {
    return ['Μετρητά', 'Κάρτα', 'Τραπεζική'][m] ?? '';
  }

  // ── Export ────────────────────────────────────────────────
  exportExcel() {
    // Απαιτεί: npm install xlsx
    import('xlsx').then(XLSX => {
      const wb = XLSX.utils.book_new();

      const chartData = this.data()?.monthlyChart ?? [];
      XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(chartData), 'Μηνιαία');

      const assetsData = this.topAssets().map(a => ({
        'Πάγιο': a.name, 'Εισπράξεις (€)': a.totalRevenue, 'Συμβόλαια': a.contractCount
      }));
      XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(assetsData), 'Top Πάγια');

      const custData = this.topCustomers().map(c => ({
        'Πελάτης': c.name, 'Υπόλοιπο (€)': c.outstandingBalance, 'Ενεργά': c.activeContracts
      }));
      XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(custData), 'Υπόλοιπα Πελατών');

      XLSX.writeFile(wb, `dashboard-${new Date().toISOString().slice(0,10)}.xlsx`);
    });
  }

  exportPdf() {
    window.print();
  }
}