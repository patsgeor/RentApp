import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TenantService } from '../../../core/services/tenant-service';
import { PlanType, UpgradeRequestDto, UpgradeRequestStatus } from '../../../types/user';

@Component({
  selector: 'app-upgrade-requests',
  imports: [DatePipe, RouterLink],
  templateUrl: './upgrade-requests.html',
})
export class UpgradeRequests implements OnInit {
  private svc = inject(TenantService);

  readonly PlanType = PlanType;

  requests  = signal<UpgradeRequestDto[]>([]);
  loading   = signal(false);
  actingId  = signal<string | null>(null);
  errorMsg  = signal('');

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.errorMsg.set('');
    this.svc.getUpgradeRequests(UpgradeRequestStatus.Pending).subscribe({
      next: r => { this.requests.set(r); this.loading.set(false); },
      error: () => { this.errorMsg.set('Σφάλμα φόρτωσης αιτημάτων.'); this.loading.set(false); }
    });
  }

  approve(r: UpgradeRequestDto) {
    this.actingId.set(r.id);
    this.svc.approveUpgradeRequest(r.id).subscribe({
      next: () => { this.requests.update(list => list.filter(x => x.id !== r.id)); this.actingId.set(null); },
      error: err => { this.errorMsg.set(err.error?.message ?? 'Σφάλμα έγκρισης.'); this.actingId.set(null); }
    });
  }

  reject(r: UpgradeRequestDto) {
    this.actingId.set(r.id);
    this.svc.rejectUpgradeRequest(r.id).subscribe({
      next: () => { this.requests.update(list => list.filter(x => x.id !== r.id)); this.actingId.set(null); },
      error: err => { this.errorMsg.set(err.error?.message ?? 'Σφάλμα απόρριψης.'); this.actingId.set(null); }
    });
  }

  planLabel(p: PlanType): string {
    const map: Record<number, string> = { [PlanType.Free]: 'Free', [PlanType.Basic]: 'Basic', [PlanType.Pro]: 'Pro' };
    return map[p] ?? '—';
  }
}
