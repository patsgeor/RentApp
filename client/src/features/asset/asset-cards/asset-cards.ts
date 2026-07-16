import { Component, inject, input, output } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { AssetDto, AssetStatus,  RateUnit } from '../../../types/asset';
import { PaginationMetadata } from '../../../types/pagination';
import { AssetService } from '../../../core/services/asset-service';
import { Paginator } from '../../../shared/paginator/paginator';

@Component({
  selector: 'app-asset-cards',
  imports: [CurrencyPipe, DatePipe, Paginator],
  templateUrl: './asset-cards.html',
})
export class AssetCards {
  items      = input.required<AssetDto[]>();
  pagination = input<PaginationMetadata | null>(null);

  pageChange = output<{ pageNumber: number; pageSize: number }>();
  deleted    = output<string>();

  private router  = inject(Router);
  private service = inject(AssetService);

  readonly AssetStatus = AssetStatus;
  readonly RateUnit  = RateUnit;

  rateUnitLabel(r: RateUnit): string {
    const map: Record<number, string> = {
      [RateUnit.PerHour]:  'ώρα',
      [RateUnit.PerDay]:   'ημέρα',
      [RateUnit.PerMonth]: 'μήνα',
      [RateUnit.Sale]:     'πώληση',
    };
    return map[r] ?? '—';
  }

  statusLabel(s: AssetStatus): string {
    const map: Record<number, string> = {
      [AssetStatus.Active]:            'Διαθέσιμο',
      [AssetStatus.UnderMaintenance]:  'Συντήρηση',
      [AssetStatus.Damaged]:           'Κατεστραμμένο',
      [AssetStatus.Retired]:           'Αποσυρμένο',
    };
    return map[s] ?? '—';
  }

  statusBadgeClass(s: AssetStatus): string {
    const map: Record<number, string> = {
       [AssetStatus.Active]:            'badge-success',
      [AssetStatus.UnderMaintenance]:  'badge-info',
      [AssetStatus.Damaged]:           'badge-error',
      [AssetStatus.Retired]:           'badge-ghost',
    };
    return `badge badge-sm ${map[s] ?? ''}`;
  }

  viewDetail(id: string) { this.router.navigate(['/assets', id]); }
  edit(id: string, e: Event) { e.stopPropagation(); this.router.navigate(['/assets', id, 'edit']); }

  delete(id: string, e: Event) {
    e.stopPropagation();
    if (confirm('Διαγραφή παγίου;')) {
      this.service.delete(id).subscribe({ next: () => this.deleted.emit(id) });
    }
  }
}
