import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs';
import { ContractService } from '../../../core/services/contract-service';
import { CustomerService } from '../../../core/services/customer-service';
import { CustomerLookupDto } from '../../../types/customers';
import {
  AvailableAssetDto, ContractAssetLineItem, ContractDetailDto,
  InstallmentFrequency, RateUnit, RentalStatus
} from '../../../types/contract';
import { DecimalPipe } from '@angular/common';

@Component({
  selector: 'app-contract-form',
  imports: [ReactiveFormsModule, RouterLink, DecimalPipe],
  templateUrl: './contract-form.html',
})
export class ContractForm implements OnInit {
  private fb          = inject(FormBuilder);
  private svc         = inject(ContractService);
  private customerSvc = inject(CustomerService);
  private route       = inject(ActivatedRoute);
  private router      = inject(Router);

  readonly RateUnit             = RateUnit;
  readonly InstallmentFrequency = InstallmentFrequency;
  readonly RentalStatus         = RentalStatus;

  isEdit          = signal(false);
  loading         = signal(false);
  saving          = signal(false);
  errorMsg        = signal('');
  private contractId: string | null = null;
  private rowVersion = 0;

  // Customer lookup
  customerSearch$  = new Subject<string>();
  customerResults  = signal<CustomerLookupDto[]>([]);
  showCustomerDrop = signal(false);
  selectedCustomer = signal<CustomerLookupDto | null>(null);

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
  }

  clearCustomer() {
    this.selectedCustomer.set(null);
    this.form.patchValue({ customerId: '', customerDisplay: '' });
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
  }

  removeLine(idx: number) {
    this.assetLines.update(lines => lines.filter((_, i) => i !== idx));
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
  onSubmit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    if (!this.form.get('customerId')!.value) {
      this.errorMsg.set('Επιλέξτε πελάτη από τη λίστα.'); return;
    }
    if (this.assetLines().length === 0) {
      this.errorMsg.set('Προσθέστε τουλάχιστον ένα πάγιο.'); return;
    }

    this.saving.set(true);
    this.errorMsg.set('');
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

    const req = this.isEdit()
      ? this.svc.update(this.contractId!, { ...basePayload, rowVersion: this.rowVersion, status: Number(f.status) as RentalStatus })
      : this.svc.create(basePayload);

    req.subscribe({
      next: () => { this.saving.set(false); this.router.navigate(['/contracts']); },
      error: (err) => { this.errorMsg.set(err.error?.message ?? 'Σφάλμα αποθήκευσης.'); this.saving.set(false); }
    });
  }
}