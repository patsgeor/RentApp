import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PaymentService } from '../../../core/services/payment-service';
import { InstallmentService } from '../../../core/services/installment-service';
import {
  AllocationItemDto, ContractPaymentDto, PaymentListItemDto,
  PaymentMatchStatus, PaymentMethod, TransactionType
} from '../../../types/payment';
import { InstallmentDto, InstallmentStatus } from '../../../types/installment';
import { PaginatedResult } from '../../../types/pagination';

type Tab = 'income' | 'expense';

@Component({
  selector: 'app-transaction-list',
  imports: [CurrencyPipe, DatePipe, RouterLink, FormsModule],
  templateUrl: './transaction-list.html',
})
export class TransactionList implements OnInit {
  private svc            = inject(PaymentService);
  private installmentSvc = inject(InstallmentService);

  readonly TransactionType   = TransactionType;
  readonly PaymentMethod     = PaymentMethod;
  readonly PaymentMatchStatus = PaymentMatchStatus;
  readonly InstallmentStatus  = InstallmentStatus;

  activeTab = signal<Tab>('income');

  // ── Αντιστοίχιση πληρωμής (auto-match / χειροκίνητη κατανομή) ──────────
  matchingPaymentId    = signal<string | null>(null);
  autoMatching         = signal<string | null>(null);
  deallocating         = signal<string | null>(null);
  matchSaving          = signal(false);
  matchError           = signal('');
  matchSuccess         = signal('');

  matchContractSearch  = signal('');
  matchContractResults = signal<ContractPaymentDto[]>([]);
  matchContractsLoading = signal(false);
  selectedMatchContract = signal<ContractPaymentDto | null>(null);
  matchInstallments     = signal<InstallmentDto[]>([]);

  matchAllocations = computed<AllocationItemDto[]>(() => {
    const paymentId = this.matchingPaymentId();
    const payment = this.incomeItems().find(p => p.id === paymentId);
    const pending = this.matchInstallments().filter(
      i => i.status !== InstallmentStatus.Paid && i.status !== InstallmentStatus.Cancelled
    );
    if (!payment || payment.unallocatedAmount <= 0 || pending.length === 0) return [];
    return this.computeFifo(payment.unallocatedAmount, pending);
  });

  // Income
  income        = signal<PaginatedResult<PaymentListItemDto> | null>(null);
  incomeLoading = signal(false);
  incomePage    = signal(1);

  // Expenses
  expenses        = signal<PaginatedResult<PaymentListItemDto> | null>(null);
  expensesLoading = signal(false);
  expensesPage    = signal(1);

  // Delete
  deleting = signal<string | null>(null);
  errorMsg = signal('');

  // Computed
  incomeItems   = computed(() => this.income()?.items   ?? []);
  expenseItems  = computed(() => this.expenses()?.items ?? []);

  incomeTotalOnPage  = computed(() => this.incomeItems().reduce((s, p) => s + p.amount, 0));
  expenseTotalOnPage = computed(() => this.expenseItems().reduce((s, p) => s + p.amount, 0));

  ngOnInit() {
    this.loadIncome();
    this.loadExpenses();
  }

  switchTab(t: Tab) {
    this.activeTab.set(t);
    this.errorMsg.set('');
  }

  loadIncome(page = this.incomePage()) {
    this.incomeLoading.set(true);
    this.svc.getIncome(page, 15).subscribe({
      next: r => { this.income.set(r); this.incomePage.set(r.metadata.currentPage); this.incomeLoading.set(false); },
      error: () => this.incomeLoading.set(false)
    });
  }

  loadExpenses(page = this.expensesPage()) {
    this.expensesLoading.set(true);
    this.svc.getExpenses(page, 15).subscribe({
      next: r => { this.expenses.set(r); this.expensesPage.set(r.metadata.currentPage); this.expensesLoading.set(false); },
      error: () => this.expensesLoading.set(false)
    });
  }

  delete(id: string) {
    if (!confirm('Διαγραφή συναλλαγής;')) return;
    this.deleting.set(id);
    this.errorMsg.set('');
    this.svc.deletePayment(id).subscribe({
      next: () => {
        this.deleting.set(null);
        if (this.activeTab() === 'income') this.loadIncome(this.incomePage());
        else this.loadExpenses(this.expensesPage());
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Σφάλμα διαγραφής.');
        this.deleting.set(null);
      }
    });
  }

  // ── Αντιστοίχιση πληρωμής ──────────────────────────────────────────────
  toggleMatch(p: PaymentListItemDto) {
    if (this.matchingPaymentId() === p.id) {
      this.matchingPaymentId.set(null);
      return;
    }
    this.matchingPaymentId.set(p.id);
    this.matchError.set('');
    this.matchSuccess.set('');
    this.matchContractSearch.set('');
    this.matchContractResults.set([]);
    this.selectedMatchContract.set(null);
    this.matchInstallments.set([]);
  }

  onMatchContractSearch(e: Event) {
    const term = (e.target as HTMLInputElement).value;
    this.matchContractSearch.set(term);
    if (!term.trim()) { this.matchContractResults.set([]); return; }
    this.matchContractsLoading.set(true);
    this.svc.getContracts(term, undefined, 1, 10).subscribe({
      next: r => { this.matchContractResults.set(r.items); this.matchContractsLoading.set(false); },
      error: () => this.matchContractsLoading.set(false)
    });
  }

  selectMatchContract(c: ContractPaymentDto) {
    this.selectedMatchContract.set(c);
    this.matchContractResults.set([]);
    this.matchContractSearch.set('');
    this.installmentSvc.getByContract(c.id).subscribe({
      next: list => this.matchInstallments.set(list)
    });
  }

  submitMatch(p: PaymentListItemDto) {
    const items = this.matchAllocations();
    if (items.length === 0) return;
    this.matchSaving.set(true);
    this.matchError.set('');
    this.installmentSvc.allocate(p.id, items).subscribe({
      next: () => {
        this.matchSaving.set(false);
        this.matchSuccess.set('Η κατανομή αποθηκεύτηκε.');
        this.matchingPaymentId.set(null);
        this.loadIncome(this.incomePage());
      },
      error: err => {
        this.matchSaving.set(false);
        this.matchError.set(err.error?.message ?? 'Σφάλμα κατανομής.');
      }
    });
  }

  autoMatch(p: PaymentListItemDto) {
    this.autoMatching.set(p.id);
    this.matchError.set('');
    this.installmentSvc.autoMatch(p.id).subscribe({
      next: r => {
        this.autoMatching.set(null);
        this.matchSuccess.set(r.message ?? '');
        this.matchingPaymentId.set(null);
        this.loadIncome(this.incomePage());
      },
      error: err => {
        this.autoMatching.set(null);
        this.matchError.set(err.error?.message ?? 'Αδυναμία αυτόματης αντιστοίχισης.');
      }
    });
  }

  doDeallocate(allocationId: string) {
    if (!confirm('Αναίρεση αυτής της κατανομής; Το ποσό θα επιστρέψει ως αδιάθετο στην πληρωμή.')) return;
    this.deallocating.set(allocationId);
    this.installmentSvc.deallocate(allocationId).subscribe({
      next: () => {
        this.deallocating.set(null);
        this.loadIncome(this.incomePage());
      },
      error: err => {
        this.deallocating.set(null);
        this.matchError.set(err.error?.message ?? 'Σφάλμα αναίρεσης κατανομής.');
      }
    });
  }

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

  allocationFor(installmentId: string): number {
    return this.matchAllocations().find(a => a.installmentId === installmentId)?.amount ?? 0;
  }

  matchStatusLabel(s: PaymentMatchStatus): string {
    const map: Record<number, string> = {
      [PaymentMatchStatus.Unmatched]:       'Μη αντιστοιχισμένη',
      [PaymentMatchStatus.AutoMatched]:     'Αυτόματη αντιστοίχιση',
      [PaymentMatchStatus.ManuallyMatched]: 'Χειροκίνητη αντιστοίχιση',
    };
    return map[s] ?? '—';
  }

  matchStatusBadge(s: PaymentMatchStatus): string {
    const map: Record<number, string> = {
      [PaymentMatchStatus.Unmatched]:       'badge-warning',
      [PaymentMatchStatus.AutoMatched]:     'badge-success',
      [PaymentMatchStatus.ManuallyMatched]: 'badge-info',
    };
    return `badge badge-xs ${map[s] ?? ''}`;
  }

  incomePages()   { return this.pages(this.income()?.metadata.totalPages   ?? 1); }
  expensePages()  { return this.pages(this.expenses()?.metadata.totalPages ?? 1); }
  private pages(n: number) { return Array.from({ length: n }, (_, i) => i + 1); }

  methodLabel(m: PaymentMethod): string {
    return ['Μετρητά', 'Κάρτα', 'Τράπεζα'][m] ?? '—';
  }

  methodBadge(m: PaymentMethod): string {
    const map: Record<number, string> = {
      [PaymentMethod.Cash]: 'badge-ghost',
      [PaymentMethod.Card]: 'badge-info',
      [PaymentMethod.BankTransfer]: 'badge-primary',
    };
    return `badge badge-sm badge-outline ${map[m] ?? ''}`;
  }
}