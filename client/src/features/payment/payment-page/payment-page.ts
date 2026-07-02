import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PaymentService } from '../../../core/services/payment-service';
import { AssetService } from '../../../core/services/asset-service';
import {
  ContractPaymentDto, PaymentListItemDto, PaymentMethod, TransactionType
} from '../../../types/payment';
import { AssetLookupDto, RentalStatus } from '../../../types/asset';
import { PaginatedResult } from '../../../types/pagination';
import { CurrencyPipe, DatePipe } from '@angular/common';

@Component({
  selector: 'app-payment-page',
  imports: [ReactiveFormsModule, DatePipe, CurrencyPipe],
  templateUrl: './payment-page.html',
})
export class PaymentPage implements OnInit {
  private paymentService = inject(PaymentService);
  private assetService  = inject(AssetService);
  private fb             = inject(FormBuilder);

  readonly PaymentMethod   = PaymentMethod;
  readonly TransactionType = TransactionType;
  readonly RentalStatus    = RentalStatus;

  // ── Tab ──────────────────────────────────────────────────────────────
  activeTab = signal<'income' | 'expense'>('income');

  // ── Contracts (Income tab) ────────────────────────────────────────────
  contracts       = signal<PaginatedResult<ContractPaymentDto> | null>(null);
  contractsLoading = signal(false);
  contractSearch  = signal('');
  contractStatus  = signal<RentalStatus | null>(null);
  contractPage    = signal(1);

  // Which contract has the payment form open
  payingContractId = signal<string | null>(null);
  incomeSaving     = signal(false);

  incomeForm = this.fb.group({
    amount:        [null as number | null, [Validators.required, Validators.min(0.01)]],
    paymentDate:   [new Date().toISOString().slice(0, 10), Validators.required],
    paymentMethod: [PaymentMethod.Cash as number],
    notes:         [''],
  });

  // ── Recent Income list ────────────────────────────────────────────────
  income        = signal<PaginatedResult<PaymentListItemDto> | null>(null);
  incomeLoading = signal(false);
  incomePage    = signal(1);

  // ── Expense tab ───────────────────────────────────────────────────────
  expenseSaving   = signal(false);
  expenseFile     = signal<File | null>(null);

  // Asset search for expense
  assetSearchTerm    = signal('');
  assetSearchResults = signal<AssetLookupDto[]>([]);
  assetSearchLoading = signal(false);
  selectedAssets     = signal<AssetLookupDto[]>([]);

  expenseForm = this.fb.group({
    amount:        [null as number | null, [Validators.required, Validators.min(0.01)]],
    paymentDate:   [new Date().toISOString().slice(0, 10), Validators.required],
    paymentMethod: [PaymentMethod.Cash as number],
    description:   ['', [Validators.required, Validators.maxLength(500)]],
    notes:         [''],
  });

  // ── Recent Expenses list ──────────────────────────────────────────────
  expenses        = signal<PaginatedResult<PaymentListItemDto> | null>(null);
  expensesLoading = signal(false);
  expensesPage    = signal(1);

  // ── Error ─────────────────────────────────────────────────────────────
  error = signal<string | null>(null);

  // ── Computed helpers ──────────────────────────────────────────────────
  contractList = computed(() => this.contracts()?.items ?? []);
  incomeList   = computed(() => this.income()?.items ?? []);
  expenseList  = computed(() => this.expenses()?.items ?? []);

  ngOnInit() {
    this.loadContracts();
    this.loadIncome();
    this.loadExpenses();
  }

  // ── Tab switch ────────────────────────────────────────────────────────
  switchTab(tab: 'income' | 'expense') {
    this.activeTab.set(tab);
    this.error.set(null);
  }

  // ── Contracts ─────────────────────────────────────────────────────────
  loadContracts() {
    this.contractsLoading.set(true);
    this.paymentService
      .getContracts(this.contractSearch(), this.contractStatus() ?? undefined, this.contractPage(), 10)
      .subscribe({
        next: r  => { this.contracts.set(r); this.contractsLoading.set(false); },
        error: () => { this.contractsLoading.set(false); this.error.set('Σφάλμα φόρτωσης συμβολαίων'); }
      });
  }

  onContractSearch(event: Event) {
    this.contractSearch.set((event.target as HTMLInputElement).value);
    this.contractPage.set(1);
    this.loadContracts();
  }

  onContractStatusChange(event: Event) {
    const v = (event.target as HTMLSelectElement).value;
    this.contractStatus.set(v === '' ? null : Number(v) as RentalStatus);
    this.contractPage.set(1);
    this.loadContracts();
  }

  contractPageChange(page: number) {
    this.contractPage.set(page);
    this.loadContracts();
  }

  // ── Pay income ────────────────────────────────────────────────────────
  openPayForm(contract: ContractPaymentDto) {
    const outstanding = contract.outstandingBalance;
    this.incomeForm.reset({
      amount:        outstanding > 0 ? outstanding : null,
      paymentDate:   new Date().toISOString().slice(0, 10),
      paymentMethod: PaymentMethod.Cash,
      notes:         '',
    });
    this.payingContractId.set(contract.id);
    this.error.set(null);
  }

  cancelPayForm() { this.payingContractId.set(null); }

  submitIncome(contractId: string) {
    if (this.incomeForm.invalid) return;
    this.incomeSaving.set(true);
    this.error.set(null);
    const v = this.incomeForm.value;
    this.paymentService.recordIncome({
      contractId,
      amount:        v.amount!,
      paymentDate:   v.paymentDate!,
      paymentMethod: Number(v.paymentMethod) as PaymentMethod,
      notes:         v.notes || undefined,
    }).subscribe({
      next: () => {
        this.payingContractId.set(null);
        this.incomeSaving.set(false);
        this.loadContracts();
        this.loadIncome();
      },
      error: err => {
        this.incomeSaving.set(false);
        this.error.set(err.error?.message ?? 'Σφάλμα αποθήκευσης');
      }
    });
  }

  // ── Income list ───────────────────────────────────────────────────────
  loadIncome() {
    this.incomeLoading.set(true);
    this.paymentService.getIncome(this.incomePage(), 10).subscribe({
      next: r  => { this.income.set(r); this.incomeLoading.set(false); },
      error: () => this.incomeLoading.set(false)
    });
  }

  incomePageChange(page: number) { this.incomePage.set(page); this.loadIncome(); }

  // ── Asset search (for expense) ────────────────────────────────────────
  onAssetSearchInput(event: Event) {
    const term = (event.target as HTMLInputElement).value.trim();
    this.assetSearchTerm.set(term);
    if (!term) { this.assetSearchResults.set([]); return; }
    this.assetSearchLoading.set(true);
    this.assetService.getAssetLookup(term).subscribe({
      next: results => {
        // exclude already-selected
        const selectedIds = new Set(this.selectedAssets().map(a => a.id));
        this.assetSearchResults.set(results.filter(r => !selectedIds.has(r.id)));
        this.assetSearchLoading.set(false);
      },
      error: () => this.assetSearchLoading.set(false)
    });
  }

  selectAsset(asset: AssetLookupDto) {
    this.selectedAssets.update(list => [...list, asset]);
    this.assetSearchResults.set([]);
    this.assetSearchTerm.set('');
  }

  removeAsset(id: string) {
    this.selectedAssets.update(list => list.filter(a => a.id !== id));
  }

  // ── File upload ───────────────────────────────────────────────────────
  onFileChange(event: Event) {
    const input = event.target as HTMLInputElement;
    this.expenseFile.set(input.files?.[0] ?? null);
  }

  clearFile() { this.expenseFile.set(null); }

  // ── Submit expense ────────────────────────────────────────────────────
  submitExpense() {
    if (this.expenseForm.invalid) return;
    this.expenseSaving.set(true);
    this.error.set(null);
    const v = this.expenseForm.value;
    this.paymentService.recordExpense(
      {
        amount:        v.amount!,
        paymentDate:   v.paymentDate!,
        paymentMethod: Number(v.paymentMethod) as PaymentMethod,
        description:   v.description!,
        notes:         v.notes || undefined,
        assetIds:      this.selectedAssets().map(a => a.id),
      },
      this.expenseFile() ?? undefined
    ).subscribe({
      next: () => {
        this.expenseForm.reset({
          amount: null, paymentDate: new Date().toISOString().slice(0, 10),
          paymentMethod: PaymentMethod.Cash, description: '', notes: ''
        });
        this.selectedAssets.set([]);
        this.expenseFile.set(null);
        this.expenseSaving.set(false);
        this.loadExpenses();
      },
      error: err => {
        this.expenseSaving.set(false);
        this.error.set(err.error?.message ?? 'Σφάλμα αποθήκευσης');
      }
    });
  }

  // ── Expenses list ─────────────────────────────────────────────────────
  loadExpenses() {
    this.expensesLoading.set(true);
    this.paymentService.getExpenses(this.expensesPage(), 10).subscribe({
      next: r  => { this.expenses.set(r); this.expensesLoading.set(false); },
      error: () => this.expensesLoading.set(false)
    });
  }

  expensesPageChange(page: number) { this.expensesPage.set(page); this.loadExpenses(); }

  // ── Helpers ───────────────────────────────────────────────────────────
  statusLabel(s: RentalStatus) {
    const map: Record<number, string> = {
      [RentalStatus.Pending]:   'Εκκρεμής',
      [RentalStatus.Active]:    'Ενεργό',
      [RentalStatus.Completed]: 'Ολοκληρωμένο',
      [RentalStatus.Cancelled]: 'Ακυρωμένο',
    };
    return map[s] ?? '—';
  }

  statusBadgeClass(s: RentalStatus) {
    const map: Record<number, string> = {
      [RentalStatus.Pending]:   'badge-warning',
      [RentalStatus.Active]:    'badge-success',
      [RentalStatus.Completed]: 'badge-ghost',
      [RentalStatus.Cancelled]: 'badge-error',
    };
    return `badge badge-sm ${map[s] ?? 'badge-ghost'}`;
  }

  paymentMethodLabel(m: PaymentMethod) {
    const map: Record<number, string> = {
      [PaymentMethod.Cash]:         'Μετρητά',
      [PaymentMethod.Card]:         'Κάρτα',
      [PaymentMethod.BankTransfer]: 'Τραπεζική Μεταφορά',
    };
    return map[m] ?? '—';
  }

  totalPages(result: PaginatedResult<unknown> | null) {
    return result?.metadata?.totalPages ?? 1;
  }

  pageRange(result: PaginatedResult<unknown> | null) {
    const total = result?.metadata?.totalPages ?? 1;
    return Array.from({ length: total }, (_, i) => i + 1);
  }
}
