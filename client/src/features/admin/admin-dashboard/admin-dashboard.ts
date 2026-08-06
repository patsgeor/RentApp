import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { TenantService } from '../../../core/services/tenant-service';
import { PlatformSummaryDto } from '../../../types/tenant';
import { PlanType } from '../../../types/user';

@Component({
  selector: 'app-admin-dashboard',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './admin-dashboard.html',
})
export class AdminDashboard implements OnInit {
  private svc = inject(TenantService);

  readonly PlanType = PlanType;

  summary  = signal<PlatformSummaryDto | null>(null);
  loading  = signal(false);
  errorMsg = signal('');

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.errorMsg.set('');
    this.svc.getPlatformSummary().subscribe({
      next: s  => { this.summary.set(s); this.loading.set(false); },
      error: () => { this.errorMsg.set('Σφάλμα φόρτωσης στατιστικών πλατφόρμας.'); this.loading.set(false); }
    });
  }

  planLabel(p: PlanType): string {
    return { [PlanType.Free]: 'Free', [PlanType.Basic]: 'Basic', [PlanType.Pro]: 'Pro' }[p] ?? '—';
  }

  planBadge(p: PlanType): string {
    const map: Record<number, string> = {
      [PlanType.Free]:  'badge-ghost',
      [PlanType.Basic]: 'badge-info',
      [PlanType.Pro]:   'badge-primary',
    };
    return `badge badge-sm ${map[p] ?? ''}`;
  }

  netResult(): number {
    const s = this.summary();
    return s ? s.platformIncome - s.platformExpenses : 0;
  }
}
