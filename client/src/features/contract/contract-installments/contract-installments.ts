import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ContractService } from '../../../core/services/contract-service';
import { InstallmentService } from '../../../core/services/installment-service';
import { ContractDetailDto } from '../../../types/contract';
import { EditLine, InstallmentDto, InstallmentStatus } from '../../../types/installment';

@Component({
  selector: 'app-contract-installments',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './contract-installments.html',
})
export class ContractInstallments implements OnInit {
  private route          = inject(ActivatedRoute);
  private contractSvc    = inject(ContractService);
  private installmentSvc = inject(InstallmentService);

  readonly IS = InstallmentStatus;

  contractId = '';
  contract   = signal<ContractDetailDto | null>(null);
  lines      = signal<EditLine[]>([]);
  loading    = signal(true);
  saving     = signal(false);
  generating = signal(false);
  notifying  = signal<string | null>(null);
  isDirty    = signal(false);
  errorMsg   = signal('');
  successMsg = signal('');

  totalScheduled = computed(() =>
    this.lines().reduce((s, l) => s + Number(l.amount) + Number(l.taxAmount), 0));
  totalPaid    = computed(() =>
    this.lines().reduce((s, l) => s + l.allocatedAmount, 0));
  outstanding  = computed(() => this.totalScheduled() - this.totalPaid());
  progress     = computed(() =>
    this.totalScheduled() > 0
      ? Math.round(this.totalPaid() / this.totalScheduled() * 100) : 0);
  paidCount    = computed(() => this.lines().filter(l => l.status === this.IS.Paid).length);
  overdueCount = computed(() => this.lines().filter(l => l.status === this.IS.Overdue).length);
  pendingCount = computed(() => this.lines().filter(l =>
    l.status === this.IS.Pending || l.status === this.IS.PartiallyPaid).length);

  ngOnInit() {
    this.contractId = this.route.snapshot.paramMap.get('id') ?? '';
    this.contractSvc.getById(this.contractId).subscribe({
      next: c => { this.contract.set(c); this.loadInstallments(); },
      error: () => { this.errorMsg.set('Αδυναμία φόρτωσης συμβολαίου.'); this.loading.set(false); }
    });
  }

  private loadInstallments() {
    this.loading.set(true);
    this.installmentSvc.getByContract(this.contractId).subscribe({
      next: list => {
        this.lines.set(list.map(i => this.toLine(i)));
        this.isDirty.set(false);
        this.loading.set(false);
      },
      error: () => { this.errorMsg.set('Αδυναμία φόρτωσης δόσεων.'); this.loading.set(false); }
    });
  }

  private toLine(i: InstallmentDto): EditLine {
    return {
      id:                i.id,
      installmentNumber: i.installmentNumber,
      periodStart:       i.periodStart.slice(0, 10),
      periodEnd:         i.periodEnd.slice(0, 10),
      dueDate:           i.dueDate.slice(0, 10),
      amount:            i.amount,
      taxAmount:         i.taxAmount,
      notes:             i.notes ?? '',
      allocatedAmount:   i.allocatedAmount,
      status:            i.status,
    };
  }

  generate() {
    if (!confirm('Αυτόματη δημιουργία δόσεων βάσει της συχνότητας του συμβολαίου;')) return;
    this.generating.set(true);
    this.errorMsg.set('');
    this.installmentSvc.generate(this.contractId).subscribe({
      next: r => {
        this.generating.set(false);
        this.successMsg.set(r.message);
        this.loadInstallments();
        setTimeout(() => this.successMsg.set(''), 4000);
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Σφάλμα δημιουργίας δόσεων.');
        this.generating.set(false);
      }
    });
  }

  addLine() {
    const last = this.lines().at(-1);
    this.lines.update(ls => [...ls, {
      installmentNumber: (last?.installmentNumber ?? 0) + 1,
      periodStart:       last?.periodEnd ?? '',
      periodEnd:         '',
      dueDate:           '',
      amount:            0,
      taxAmount:         0,
      notes:             '',
      allocatedAmount:   0,
      status:            this.IS.Pending,
    }]);
    this.isDirty.set(true);
  }

  removeLine(idx: number) {
    const l = this.lines()[idx];
    if (l.allocatedAmount > 0) {
      this.errorMsg.set(`Η δόση #${l.installmentNumber} έχει πληρωμές και δεν μπορεί να διαγραφεί.`);
      return;
    }
    this.lines.update(ls => ls.filter((_, i) => i !== idx));
    this.renumber();
    this.isDirty.set(true);
  }

  updateLine(idx: number, field: keyof EditLine, value: string | number) {
    this.lines.update(ls => {
      const c = [...ls];
      c[idx] = { ...c[idx], [field]: value };
      return c;
    });
    this.isDirty.set(true);
  }

  private renumber() {
    this.lines.update(ls => ls.map((l, i) => ({ ...l, installmentNumber: i + 1 })));
  }

  lineTotal(l: EditLine) {
    return Number(l.amount) + Number(l.taxAmount);
  }

  canEditAmounts(l: EditLine) {
    return l.status !== this.IS.Paid && l.allocatedAmount === 0;
  }

  save() {
    this.saving.set(true);
    this.errorMsg.set('');
    const schedule = this.lines().map(l => ({
      id:                l.id,
      installmentNumber: l.installmentNumber,
      periodStart:       l.periodStart,
      periodEnd:         l.periodEnd,
      dueDate:           l.dueDate,
      amount:            Number(l.amount),
      taxAmount:         Number(l.taxAmount),
      notes:             l.notes || undefined,
    }));
    this.installmentSvc.updateSchedule(this.contractId, schedule).subscribe({
      next: r => {
        this.successMsg.set(r.message);
        this.saving.set(false);
        this.loadInstallments();
        setTimeout(() => this.successMsg.set(''), 4000);
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Σφάλμα αποθήκευσης.');
        this.saving.set(false);
      }
    });
  }

  revert() {
    if (this.isDirty() && !confirm('Απόρριψη όλων των αλλαγών;')) return;
    this.loadInstallments();
  }

  sendEmail(id: string) {
    this.notifying.set(id);
    this.errorMsg.set('');
    this.installmentSvc.notifyEmail(id).subscribe({
      next: r => {
        this.successMsg.set(r.message);
        this.notifying.set(null);
        setTimeout(() => this.successMsg.set(''), 4000);
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Σφάλμα αποστολής email.');
        this.notifying.set(null);
      }
    });
  }

  statusLabel(s: InstallmentStatus) {
    const map: Record<number, string> = {
      [this.IS.Pending]:       'Εκκρεμής',
      [this.IS.PartiallyPaid]: 'Μερικώς',
      [this.IS.Paid]:          'Εξοφλημένη',
      [this.IS.Overdue]:       'Ληξ/θεσμη',
      [this.IS.Cancelled]:     'Ακυρ.',
    };
    return map[s] ?? '—';
  }

  statusBadge(s: InstallmentStatus) {
    const map: Record<number, string> = {
      [this.IS.Pending]:       'badge-warning',
      [this.IS.PartiallyPaid]: 'badge-info',
      [this.IS.Paid]:          'badge-success',
      [this.IS.Overdue]:       'badge-error',
      [this.IS.Cancelled]:     'badge-ghost',
    };
    return `badge badge-sm ${map[s] ?? ''}`;
  }
}