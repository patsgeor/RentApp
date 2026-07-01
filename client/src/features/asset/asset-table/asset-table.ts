import { Component, inject, input, output } from '@angular/core';
import { DatePipe, CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import { AssetDto, AssetStatus, AcquisitionType } from '../../../types/asset';
import { AssetService } from '../../../core/services/asset-service';
import { Paginator } from '../../../shared/paginator/paginator';
import { PaginationMetadata } from '../../../types/pagination';

@Component({
  selector: 'app-asset-table',
  imports: [DatePipe, CurrencyPipe, Paginator],
  templateUrl: './asset-table.html',
})
export class AssetTable {
  items      = input.required<AssetDto[]>();
  pagination = input<PaginationMetadata | null>(null);

  pageChange = output<{ pageNumber: number; pageSize: number }>();
  deleted    = output<string>();

  private router  = inject(Router);
  private service = inject(AssetService);

  readonly AssetStatus     = AssetStatus;
  readonly AcquisitionType = AcquisitionType;

  statusLabel(s: AssetStatus): string {
    const map: Record<number, string> = {
      [AssetStatus.Available]: 'Διαθέσιμο',
      [AssetStatus.Rented]: 'Ενοικιασμένο',
      [AssetStatus.UnderMaintenance]: 'Συντήρηση',
      [AssetStatus.Damaged]: 'Κατεστραμμένο',
    };
    return map[s] ?? '—';
  }

  statusBadgeClass(s: AssetStatus): string {
    const map: Record<number, string> = {
      [AssetStatus.Available]: 'badge-success',
      [AssetStatus.Rented]: 'badge-warning',
      [AssetStatus.UnderMaintenance]: 'badge-info',
      [AssetStatus.Damaged]: 'badge-error',
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
