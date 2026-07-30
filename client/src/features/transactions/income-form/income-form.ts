import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PaymentService } from '../../../core/services/payment-service';
import { AllocationItemDto, ContractPaymentDto, PaymentMethod } from '../../../types/payment';
import { RentalStatus } from '../../../types/asset';
import { InstallmentService } from '../../../core/services/installment-service';
import { InstallmentDto, InstallmentStatus } from '../../../types/installment';
import { map } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-income-form',
  imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, RouterLink,DecimalPipe],
  templateUrl: './income-form.html',
})
export class IncomeForm implements OnInit {
  private svc             = inject(PaymentService);
  private installmentSvc  = inject(InstallmentService);
  private fb              = inject(FormBuilder);

  readonly PaymentMethod     = PaymentMethod;
  readonly RentalStatus      = RentalStatus;
  readonly InstallmentStatus = InstallmentStatus;

  contracts    = signal<ContractPaymentDto[]>([]);
  totalPages   = signal(1);
  currentPage  = signal(1);
  totalCount   = signal(0);
  loading      = signal(false);
  search       = signal('');
  statusFilter = signal<number | null>(null);

  selected     = signal<ContractPaymentDto | null>(null);
  installments = signal<InstallmentDto[]>([]);  saving   = signal(false);

  success  = signal(false);
  errorMsg = signal('');

  form = this.fb.group({
    amount:        [null as number | null, [Validators.required, Validators.min(0.01)]],
    paymentDate:   [new Date().toISOString().slice(0, 10), Validators.required],
    paymentMethod: [PaymentMethod.Cash as number],
    notes:         [''],
  });

  // Bridge form amount to a signal so computed() can track it
  private amountSignal = toSignal(
    this.form.get('amount')!.valueChanges.pipe(map(v => v ?? 0)),
    { initialValue: 0 }
  );

  fifoAllocations = computed<AllocationItemDto[]>(() => {
    const amount = this.amountSignal();
    const pending = this.installments().filter(
      i => i.status !== InstallmentStatus.Paid && i.status !== InstallmentStatus.Cancelled
    );
    if (!amount || amount <= 0 || pending.length === 0) return [];
    return this.computeFifo(amount, pending);
  });
  
  ngOnInit() { this.load(); }

  load(page = 1) {
    this.loading.set(true);
    this.svc.getContracts(this.search(), this.statusFilter() ?? undefined, page, 10).subscribe({
      next: r => {
        this.contracts.set(r.items);
        this.totalPages.set(r.metadata.totalPages);
        this.currentPage.set(r.metadata.currentPage);
        this.totalCount.set(r.metadata.totalCount);
        this.loading.set(false);
        const cur = this.selected();
        if (cur) {
          const refreshed = r.items.find(c => c.id === cur.id);
          if (refreshed) this.selected.set(refreshed);
        }
      },
      error: () => this.loading.set(false)
    });
  }

  onSearch(e: Event) {
    this.search.set((e.target as HTMLInputElement).value);
    this.load(1);
  }

  onStatus(e: Event) {
    const v = (e.target as HTMLSelectElement).value;
    this.statusFilter.set(v === '' ? null : Number(v));
    this.load(1);
  }

  select(c: ContractPaymentDto) {
    this.selected.set(c);
    this.installments.set([]);
    this.errorMsg.set('');
    this.success.set(false);
    this.form.patchValue({ amount: c.outstandingBalance > 0 ? c.outstandingBalance : null });
    this.installmentSvc.getByContract(c.id).subscribe({
      next: list => this.installments.set(list),
    });
  }

  clearSelected() {
    this.selected.set(null);
    this.installments.set([]);
  }

  onAmountInput() {
    // signal-based computed reacts automatically — this is a no-op hook for the template
  }

  submit() {
    if (this.form.invalid || !this.selected()) { this.form.markAllAsTouched(); return; }
    this.saving.set(true);
    this.errorMsg.set('');
    this.success.set(false);
    const v = this.form.value;
    this.svc.recordIncome({
      amount:        v.amount!,
      paymentDate:   v.paymentDate!,
      paymentMethod: Number(v.paymentMethod) as PaymentMethod,
      notes:         v.notes || undefined,
      allocations:   this.fifoAllocations().length > 0 ? this.fifoAllocations() : undefined,
    }).subscribe({
      next: () => {
        this.success.set(true);
        this.saving.set(false);
        this.form.patchValue({ amount: null, notes: '' });
        this.load(this.currentPage());
        // Refresh installments
        const sel = this.selected();
        if (sel) this.installmentSvc.getByContract(sel.id).subscribe({
          next: list => this.installments.set(list),
        });
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Σφάλμα αποθήκευσης.');
        this.saving.set(false);
      }
    });
  }

  installmentStatusLabel(s: InstallmentStatus): string {
    const map: Record<number, string> = {
      [InstallmentStatus.Pending]:       'Εκκρεμής',
      [InstallmentStatus.PartiallyPaid]: 'Μερική',
      [InstallmentStatus.Paid]:          'Εξοφλημένη',
      [InstallmentStatus.Overdue]:       'Ληξιπρόθεσμη',
      [InstallmentStatus.Cancelled]:     'Ακυρωμένη',
    };
    return map[s] ?? '—';
  }

  installmentStatusBadge(s: InstallmentStatus): string {
    const map: Record<number, string> = {
      [InstallmentStatus.Pending]:       'badge-warning',
      [InstallmentStatus.PartiallyPaid]: 'badge-info',
      [InstallmentStatus.Paid]:          'badge-success',
      [InstallmentStatus.Overdue]:       'badge-error',
      [InstallmentStatus.Cancelled]:     'badge-ghost',
    };
    return `badge badge-xs ${map[s] ?? ''}`;
  }

  allocationFor(installmentId: string): number {
    return this.fifoAllocations().find(a => a.installmentId === installmentId)?.amount ?? 0;
  }

  statusLabel(s: RentalStatus): string {
    const map: Record<number, string> = {
      [RentalStatus.Pending]: 'Εκκρεμής', [RentalStatus.Active]: 'Ενεργό',
      [RentalStatus.Completed]: 'Ολοκλ.', [RentalStatus.Cancelled]: 'Ακυρωμένο',
    };
    return map[s] ?? '—';
  }

  statusBadge(s: RentalStatus): string {
    const map: Record<number, string> = {
      [RentalStatus.Pending]: 'badge-warning', [RentalStatus.Active]: 'badge-success',
      [RentalStatus.Completed]: 'badge-ghost',  [RentalStatus.Cancelled]: 'badge-error',
    };
    return `badge badge-sm ${map[s] ?? ''}`;
  }

  pages() { return Array.from({ length: this.totalPages() }, (_, i) => i + 1); }

  
  private computeFifo(amount: number, pending: InstallmentDto[]): AllocationItemDto[] {
    const sorted = [...pending].sort(
      (a, b) => new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime()
    );
    const result: AllocationItemDto[] = [];
    let remaining = amount;
    for (const inv of sorted) {
      if (remaining <= 0) break;
      const outstanding = inv.totalAmount - inv.allocatedAmount;
      if (outstanding <= 0) continue;
      const toAllocate = Math.min(remaining, outstanding);
      result.push({ installmentId: inv.id, amount: Math.round(toAllocate * 100) / 100 });
      remaining -= toAllocate;
    }
    return result;
  }

}