import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs';
import { ContractService } from '../../../core/services/contract-service';
import { CustomerService } from '../../../core/services/customer-service';
import { InstallmentService } from '../../../core/services/installment-service';
import { CustomerLookupDto, ContactDto } from '../../../types/customers';
import {
  AvailableAssetDto, ContractAssetLineItem, ContractDetailDto,
  InstallmentFrequency, RateUnit, RentalStatus
} from '../../../types/contract';
import { InstallmentDto, InstallmentStatus } from '../../../types/installment';
import { DatePipe, DecimalPipe, Location } from '@angular/common';

@Component({
  selector: 'app-contract-form',
  imports: [ReactiveFormsModule, RouterLink, DecimalPipe, DatePipe],
  templateUrl: './contract-form.html',
})
export class ContractForm implements OnInit {
  private fb             = inject(FormBuilder);
  private svc            = inject(ContractService);
  private customerSvc    = inject(CustomerService);
  private installmentSvc = inject(InstallmentService);
  private route          = inject(ActivatedRoute);
  private router         = inject(Router);
  private location       = inject(Location);

  readonly RateUnit             = RateUnit;
  readonly InstallmentFrequency = InstallmentFrequency;
  readonly RentalStatus         = RentalStatus;
  readonly InstallmentStatus    = InstallmentStatus;

  // Αποστολή συμβολαίου με email
  showEmailForm  = signal(false);
  sendingEmail   = signal(false);
  emailMsg       = signal('');
  emailError     = signal('');
  emailFiles     = signal<File[]>([]);

  emailForm = this.fb.group({
    to:               ['', Validators.email],
    subject:          [''],
    message:          [''],
    activateContract: [true],
  });

  // Δόσεις (μόνο σε αποθηκευμένο συμβόλαιο)
  installments        = signal<InstallmentDto[]>([]);
  installmentsLoading = signal(false);
  generatingSchedule  = signal(false);
  scheduleMsg         = signal('');
  scheduleError       = signal('');

  isEdit          = signal(false);
  loading         = signal(false);
  saving          = signal(false);
  errorMsg        = signal('');
  contractId: string | null = null;
  private rowVersion = 0;

  // Customer lookup
  customerSearch$  = new Subject<string>();
  customerResults  = signal<CustomerLookupDto[]>([]);
  showCustomerDrop = signal(false);
  selectedCustomer = signal<CustomerLookupDto | null>(null);
  // Επαφές του επιλεγμένου πελάτη — για προσυμπλήρωση email αποστολής συμβολαίου
  customerContacts = signal<ContactDto[]>([]);

  // Available assets
  availableAssets = signal<AvailableAssetDto[]>([]);
  assetsLoading   = signal(false);
  assetsLoaded    = signal(false);

  // Asset picker search + pagination
  assetSearch    = signal('');
  assetPage      = signal(1);
  readonly assetsPerPage = 12;

  filteredAssets = computed(() => {
    const q = this.assetSearch().toLowerCase().trim();
    if (!q) return this.availableAssets();
    return this.availableAssets().filter(a =>
      a.name.toLowerCase().includes(q) ||
      (a.assetTypeName?.toLowerCase().includes(q) ?? false)
    );
  });

  assetTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredAssets().length / this.assetsPerPage))
  );

  pagedAssets = computed(() => {
    const page = Math.min(this.assetPage(), this.assetTotalPages());
    const start = (page - 1) * this.assetsPerPage;
    return this.filteredAssets().slice(start, start + this.assetsPerPage);
  });

  // Selected asset lines
  assetLines = signal<ContractAssetLineItem[]>([]);

  // Computed totals
  subtotal = computed(() => this.assetLines().reduce((s, a) => s + a.calculatedAmount, 0));
  total    = computed(() => {
    const d = Number(this.form.get('discountAmount')?.value ?? 0) || 0;
    const t = Number(this.form.get('taxAmount')?.value ?? 0) || 0;
    return this.subtotal() - d + t;
  });

  form = this.fb.group({
    customerId:           ['', Validators.required],
    customerDisplay:      [''],
    startDate:            ['', Validators.required],
    endDate:              ['', Validators.required],
    signedDate:           [''],
    referenceCode:        [''],
    discountAmount:       [0, [Validators.min(0)]],
    taxAmount:            [0, [Validators.min(0)]],
    installmentFrequency: [InstallmentFrequency.Monthly as number],
    status:               [RentalStatus.Pending as number],
    notes:                [''],
    terms:                [''],
  });

  ngOnInit() {
    this.customerSearch$.pipe(debounceTime(300), distinctUntilChanged()).subscribe(q => {
      if (q.length < 1) { this.customerResults.set([]); return; }
      this.customerSvc.getLookup(q).subscribe(r => this.customerResults.set(r));
    });

    this.form.get('startDate')!.valueChanges.subscribe(() => this.onDatesChange());
    this.form.get('endDate')!.valueChanges.subscribe(() => this.onDatesChange());

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.contractId = id;
      this.loading.set(true);
      this.svc.getById(id).subscribe({
        next: dto => this.patchForm(dto),
        error: () => { this.errorMsg.set('Αδυναμία φόρτωσης συμβολαίου.'); this.loading.set(false); }
      });
    }
  }

  private patchForm(dto: ContractDetailDto) {
    this.rowVersion = dto.rowVersion;
    this.selectedCustomer.set({ id: dto.customerId, name: dto.customerName, afm: '' });
    this.loadCustomerContacts(dto.customerId);

    this.form.patchValue({
      customerId:           dto.customerId,
      customerDisplay:      dto.customerName,
      startDate:            dto.startDate.slice(0, 16),
      endDate:              dto.endDate.slice(0, 16),
      signedDate:           dto.signedDate ? dto.signedDate.slice(0, 10) : '',
      referenceCode:        dto.referenceCode ?? '',
      discountAmount:       dto.discountAmount,
      taxAmount:            dto.taxAmount,
      installmentFrequency: dto.installmentFrequency,
      status:               dto.status,
      notes:                dto.notes ?? '',
      terms:                dto.terms ?? '',
    });

    this.assetLines.set(dto.assets.map(a => ({
      assetId:          a.assetId,
      assetName:        a.assetName,
      startDate:        a.startDate.slice(0, 16),
      endDate:          a.endDate.slice(0, 16),
      unitCost:         a.unitCost,
      rateUnit:         a.rateUnit,
      calculatedAmount: a.calculatedAmount,
      notes:            a.notes ?? '',
    })));

    this.loading.set(false);
    this.loadAvailableAssets();
    this.loadInstallments();
  }

  // ── Δόσεις ─────────────────────────────────────────────────────────────
  private loadInstallments() {
    if (!this.contractId) return;
    this.installmentsLoading.set(true);
    this.installmentSvc.getByContract(this.contractId).subscribe({
      next: list => { this.installments.set(list); this.installmentsLoading.set(false); },
      error: () => this.installmentsLoading.set(false)
    });
  }

  generateSchedule() {
    if (!this.contractId) return;
    const freq = this.freqLabel(Number(this.form.get('installmentFrequency')!.value) as InstallmentFrequency);
    if (!confirm(`Δημιουργία δόσεων με συχνότητα «${freq}»; Οι υπάρχουσες δόσεις χωρίς πληρωμές θα αντικατασταθούν.`)) return;

    this.generatingSchedule.set(true);
    this.scheduleError.set('');
    this.scheduleMsg.set('');

    // Το backend διαβάζει συχνότητα/διάρκεια από τη βάση — αποθηκεύουμε πρώτα ώστε
    // να χρησιμοποιηθούν οι τιμές που βλέπει ο χρήστης στη φόρμα, όχι οι παλιές.
    this.saveCurrent().subscribe({
      next: () => {
        this.installmentSvc.generate(this.contractId!).subscribe({
          next: r => {
            this.generatingSchedule.set(false);
            this.scheduleMsg.set(r.message);
            this.reloadContract();
          },
          error: err => {
            this.generatingSchedule.set(false);
            this.scheduleError.set(err.error?.message ?? 'Σφάλμα δημιουργίας δόσεων.');
          }
        });
      },
      error: err => {
        this.generatingSchedule.set(false);
        this.scheduleError.set(err.error?.message ?? 'Σφάλμα αποθήκευσης συμβολαίου.');
      }
    });
  }

  private reloadContract() {
    this.svc.getById(this.contractId!).subscribe({
      next: dto => { this.rowVersion = dto.rowVersion; this.loadInstallments(); }
    });
  }

  scheduledTotal() {
    return this.installments().reduce((s, i) => s + i.totalAmount, 0);
  }

  // ── Αποστολή με email ──────────────────────────────────────────────────
  toggleEmailForm() {
    this.showEmailForm.update(v => !v);
    this.emailMsg.set('');
    this.emailError.set('');
    if (this.showEmailForm()) {
      const emails = this.contactEmails();
      this.emailForm.patchValue({
        to:      emails[0]?.email ?? '',
        subject: `Συμβόλαιο Μίσθωσης${this.form.get('referenceCode')?.value ? ' — ' + this.form.get('referenceCode')!.value : ''}`,
      });
    }
  }

  onEmailFilesChange(event: Event) {
    const input = event.target as HTMLInputElement;
    this.emailFiles.set(Array.from(input.files ?? []));
  }

  removeEmailFile(idx: number) {
    this.emailFiles.update(list => list.filter((_, i) => i !== idx));
  }

  sendContractEmail() {
    if (this.emailForm.invalid) { this.emailForm.markAllAsTouched(); return; }
    if (!this.form.get('customerId')!.value) {
      this.emailError.set('Επιλέξτε πελάτη από τη λίστα.'); return;
    }
    if (this.assetLines().length === 0) {
      this.emailError.set('Προσθέστε τουλάχιστον ένα πάγιο.'); return;
    }

    this.sendingEmail.set(true);
    this.emailError.set('');
    this.emailMsg.set('');

    // Αποθηκεύουμε πρώτα — και σε νέο (μη αποθηκευμένο ακόμα) συμβόλαιο — ώστε
    // το email να αντανακλά ακριβώς ό,τι βλέπει ο χρήστης στη φόρμα.
    this.saveCurrent().subscribe({
      next: saved => this.doSendEmail(saved.id, saved.rowVersion),
      error: err => {
        this.sendingEmail.set(false);
        this.emailError.set(err.error?.message ?? 'Σφάλμα αποθήκευσης συμβολαίου.');
      }
    });
  }

  private doSendEmail(contractId: string, rowVersion: number) {
    const wasNew = !this.isEdit();
    this.contractId = contractId;
    this.rowVersion = rowVersion;
    this.isEdit.set(true);
    if (wasNew) this.location.replaceState(`/contracts/${contractId}/edit`);

    const v = this.emailForm.value;
    this.svc.sendEmail(contractId, {
      to:               v.to || undefined,
      subject:          v.subject || undefined,
      message:          v.message || undefined,
      activateContract: v.activateContract ?? true,
    }, this.emailFiles()).subscribe({
      next: r => {
        this.sendingEmail.set(false);
        this.emailMsg.set(wasNew
          ? 'Το συμβόλαιο αποθηκεύτηκε και ' + (r.message ?? 'στάλθηκε.').toLowerCase()
          : (r.message ?? 'Το email στάλθηκε.'));
        this.emailFiles.set([]);
        this.showEmailForm.set(false);
        if (r.statusChanged) {
          this.form.patchValue({ status: RentalStatus.Active }, { emitEvent: false });
        }
        this.reloadContract();
      },
      error: err => {
        this.sendingEmail.set(false);
        this.emailError.set(err.error?.message ?? 'Αποτυχία αποστολής email.');
      }
    });
  }

  installmentStatusLabel(s: InstallmentStatus): string {
    const map: Record<number, string> = {
      [InstallmentStatus.Pending]:       'Εκκρεμής',
      [InstallmentStatus.PartiallyPaid]: 'Μερικώς',
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

  // ── Customer lookup ────────────────────────────────────────────────────
  onCustomerInput(e: Event) {
    const val = (e.target as HTMLInputElement).value;
    this.form.patchValue({ customerDisplay: val, customerId: '' });
    this.selectedCustomer.set(null);
    this.showCustomerDrop.set(true);
    this.customerSearch$.next(val);
  }

  selectCustomer(c: CustomerLookupDto) {
    this.selectedCustomer.set(c);
    this.form.patchValue({ customerId: c.id, customerDisplay: `${c.name} (${c.afm})` });
    this.showCustomerDrop.set(false);
    this.customerResults.set([]);
    this.loadCustomerContacts(c.id);
  }

  clearCustomer() {
    this.selectedCustomer.set(null);
    this.customerContacts.set([]);
    this.form.patchValue({ customerId: '', customerDisplay: '' });
  }

  private loadCustomerContacts(customerId: string) {
    this.customerContacts.set([]);
    this.customerSvc.getById(customerId).subscribe({
      next: c => this.customerContacts.set(c.contacts ?? [])
    });
  }

  /** Emails επαφών του πελάτη που έχουν καταχωρημένο email — για προσυμπλήρωση/επιλογή στη φόρμα αποστολής. */
  contactEmails(): { name: string; email: string }[] {
    return this.customerContacts()
      .filter(c => !!c.email)
      .map(c => ({ name: c.name, email: c.email! }));
  }

  selectContactEmail(email: string) {
    this.emailForm.patchValue({ to: email });
  }

  // ── Available assets ───────────────────────────────────────────────────
  private onDatesChange() {
    const s = this.form.get('startDate')!.value;
    const e = this.form.get('endDate')!.value;
    if (s && e && e > s) {
      this.loadAvailableAssets();
    }
  }

  private loadAvailableAssets() {
    const s = this.form.get('startDate')!.value;
    const e = this.form.get('endDate')!.value;
    if (!s || !e) return;

    this.assetsLoading.set(true);
    this.assetsLoaded.set(false);
    this.svc.getAvailableAssets(s, e, this.contractId ?? undefined).subscribe({
      next: assets => {
        this.availableAssets.set(assets);
        this.assetPage.set(1);
        this.assetSearch.set('');
        this.assetsLoading.set(false);
        this.assetsLoaded.set(true);
      },
      error: () => this.assetsLoading.set(false)
    });
  }

  onAssetSearch(e: Event) {
    this.assetSearch.set((e.target as HTMLInputElement).value);
    this.assetPage.set(1);
  }

  assetPageChange(p: number) {
    this.assetPage.set(p);
  }

  isAssetSelected(id: string) {
    return this.assetLines().some(l => l.assetId === id);
  }

  toggleAsset(asset: AvailableAssetDto) {
    if (this.isAssetSelected(asset.id)) {
      this.assetLines.update(lines => lines.filter(l => l.assetId !== asset.id));
    } else {
      const start = this.form.get('startDate')!.value ?? '';
      const end   = this.form.get('endDate')!.value ?? '';
      const calc  = this.calcAmount(asset.rateUnit, asset.cost, start, end);
      this.assetLines.update(lines => [...lines, {
        assetId:          asset.id,
        assetName:        asset.name,
        assetTypeName:    asset.assetTypeName,
        startDate:        start,
        endDate:          end,
        unitCost:         asset.cost,
        rateUnit:         asset.rateUnit,
        calculatedAmount: calc,
        notes:            '',
      }]);
    }
    this.syncContractDatesFromLines();
  }

  // Οι ημερομηνίες του συμβολαίου ακολουθούν πάντα τα πάγια: έναρξη = η νωρίτερη,
  // λήξη = η αργότερη. emitEvent:false ώστε να μην ξαναφορτωθεί η λίστα διαθέσιμων
  // παγίων και χαθεί η τρέχουσα επιλογή του χρήστη.
  private syncContractDatesFromLines() {
    const lines = this.assetLines();
    if (lines.length === 0) return;

    const starts = lines.map(l => l.startDate).filter(Boolean);
    const ends   = lines.map(l => l.endDate).filter(Boolean);
    if (starts.length === 0 || ends.length === 0) return;

    this.form.patchValue({
      startDate: starts.reduce((a, b) => (a < b ? a : b)),
      endDate:   ends.reduce((a, b) => (a > b ? a : b)),
    }, { emitEvent: false });
  }

  updateLine(idx: number, field: keyof ContractAssetLineItem, value: string | number) {
    this.assetLines.update(lines => {
      const copy = [...lines];
      const line = { ...copy[idx], [field]: value };
      if (['unitCost', 'rateUnit', 'startDate', 'endDate'].includes(field as string)) {
        line.calculatedAmount = this.calcAmount(
          Number(line.rateUnit) as RateUnit,
          Number(line.unitCost),
          line.startDate,
          line.endDate
        );
      }
      copy[idx] = line;
      return copy;
    });
    if (field === 'startDate' || field === 'endDate') this.syncContractDatesFromLines();
  }

  removeLine(idx: number) {
    this.assetLines.update(lines => lines.filter((_, i) => i !== idx));
    this.syncContractDatesFromLines();
  }

  calcAmount(rateUnit: RateUnit, unitCost: number, start: string, end: string): number {
    if (!start || !end || unitCost <= 0) return 0;
    const s = new Date(start);
    const e = new Date(end);
    if (e.getTime() <= s.getTime()) return 0;

    if (rateUnit === RateUnit.PerMonth) {
      return Math.round(unitCost * this.calcMonths(s, e) * 100) / 100;
    }

    const ms = e.getTime() - s.getTime();
    switch (rateUnit) {
      case RateUnit.PerHour:  return Math.round(unitCost * ms / 3_600_000 * 100) / 100;
      case RateUnit.PerDay:   return Math.round(unitCost * ms / 86_400_000 * 100) / 100;
      case RateUnit.Sale:     return unitCost;
      default:                return 0;
    }
  }

  private calcMonths(start: Date, end: Date): number {
    const sd = start.getDate(), sm = start.getMonth(), sy = start.getFullYear();
    const ed = end.getDate(),   em = end.getMonth(),   ey = end.getFullYear();
    let months = (ey - sy) * 12 + (em - sm);
    if (ed < sd) {
      months--;
      const daysInPrevMonth = new Date(ey, em, 0).getDate();
      return months + ((daysInPrevMonth - sd) + ed) / 30;
    }
    return months + (ed - sd) / 30;
  }

  durationLabel(start: string, end: string): string {
    if (!start || !end) return '';
    const ms = new Date(end).getTime() - new Date(start).getTime();
    if (ms <= 0) return '';
    const hours = ms / 3_600_000;
    const days  = ms / 86_400_000;
    if (hours < 24)  return `${Math.round(hours * 10) / 10} ώρ.`;
    if (days  < 30)  return `${Math.round(days  * 10) / 10} ημ.`;
    return `${Math.round(this.calcMonths(new Date(start), new Date(end)) * 10) / 10} μήν.`;
  }

  rateUnitLabel(r: RateUnit) {
    return { [RateUnit.PerHour]: '/ώρα', [RateUnit.PerDay]: '/ημέρα', [RateUnit.PerMonth]: '/μήνα', [RateUnit.Sale]: 'εφάπαξ' }[r] ?? '';
  }

  freqLabel(f: InstallmentFrequency) {
    const map: Record<number, string> = {
      [InstallmentFrequency.Monthly]: 'Μηνιαία', [InstallmentFrequency.Weekly]: 'Εβδομαδιαία',
      [InstallmentFrequency.Quarterly]: 'Τριμηνιαία', [InstallmentFrequency.Yearly]: 'Ετήσια',
      [InstallmentFrequency.OneTime]: 'Εφάπαξ',
    };
    return map[f] ?? '';
  }

  // ── Submit ─────────────────────────────────────────────────────────────
  private saveCurrent() {
    const f = this.form.value;

    const assets = this.assetLines().map(l => ({
      assetId:          l.assetId,
      startDate:        l.startDate,
      endDate:          l.endDate,
      unitCost:         Number(l.unitCost),
      rateUnit:         Number(l.rateUnit) as RateUnit,
      calculatedAmount: l.calculatedAmount,
      notes:            l.notes || undefined,
    }));

    const basePayload = {
      customerId:           f.customerId!,
      startDate:            f.startDate!,
      endDate:              f.endDate!,
      signedDate:           f.signedDate || undefined,
      referenceCode:        f.referenceCode || undefined,
      taxAmount:            Number(f.taxAmount) || 0,
      discountAmount:       Number(f.discountAmount) || 0,
      installmentFrequency: Number(f.installmentFrequency) as InstallmentFrequency,
      notes:                f.notes || undefined,
      terms:                f.terms || undefined,
      assets,
    };

    return this.isEdit()
      ? this.svc.update(this.contractId!, { ...basePayload, rowVersion: this.rowVersion, status: Number(f.status) as RentalStatus })
      : this.svc.create(basePayload);
  }

  onSubmit(generateAfterSave = false) {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    if (!this.form.get('customerId')!.value) {
      this.errorMsg.set('Επιλέξτε πελάτη από τη λίστα.'); return;
    }
    if (this.assetLines().length === 0) {
      this.errorMsg.set('Προσθέστε τουλάχιστον ένα πάγιο.'); return;
    }

    this.saving.set(true);
    this.errorMsg.set('');

    const req = this.saveCurrent();

    req.subscribe({
      next: (saved) => {
        this.saving.set(false);
        if (!generateAfterSave) { this.router.navigate(['/contracts']); return; }

        // Οι δόσεις χρειάζονται αποθηκευμένο συμβόλαιο — μένουμε στη σελίδα
        // επεξεργασίας του ώστε ο χρήστης να δει αμέσως το πρόγραμμα δόσεων.
        this.installmentSvc.generate(saved.id).subscribe({
          next: () => this.router.navigate(['/contracts', saved.id, 'edit']),
          error: () => this.router.navigate(['/contracts', saved.id, 'edit'])
        });
      },
      error: (err) => { this.errorMsg.set(err.error?.message ?? 'Σφάλμα αποθήκευσης.'); this.saving.set(false); }
    });
  }
}