import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe, CurrencyPipe } from '@angular/common';
import { AssetService } from '../../../core/services/asset-service';
import { AssetDetailDto, AssetStatus, RateUnit, AssetTypeFieldDto, FieldDataType } from '../../../types/asset';
import { AssetMaintenanceHistory } from '../asset-maintenance-history/asset-maintenance-history';
import { AssetRentalHistory } from '../asset-rental-history/asset-rental-history';
import { AssetQrCode } from "../asset-qr-code/asset-qr-code";
import { AssetCalendar } from "../asset-calendar/asset-calendar";


@Component({
  selector: 'app-asset-detail',
  imports: [RouterLink, DatePipe, CurrencyPipe, AssetMaintenanceHistory, AssetRentalHistory, AssetQrCode, AssetCalendar],
  templateUrl: './asset-detail.html',
})
export class AssetDetail implements OnInit {
  private fb      = inject(FormBuilder);
  private route   = inject(ActivatedRoute);
  private router  = inject(Router);
  private service = inject(AssetService);

  readonly AssetStatus = AssetStatus;
  readonly RateUnit    = RateUnit;
  readonly FieldDataType = FieldDataType;

    asset   = signal<AssetDetailDto | null>(null);
  schema  = signal<AssetTypeFieldDto[]>([]);
  loading = signal(true);
  assetId = signal('');

  maintForm = this.fb.group({
    date:         [new Date().toISOString().substring(0, 10), Validators.required],
    description:  ['', [Validators.required, Validators.maxLength(250)]],
    cost:         [0 as number, [Validators.required, Validators.min(0)]],
    maintainedBy: ['', Validators.maxLength(100)],
  });

  get mf() { return this.maintForm.controls; }

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.assetId.set(id);
    this.service.getById(id).subscribe({
      next: (asset) => {
        this.asset.set(asset);
        this.service.getAssetTypeById(asset.assetTypeId).subscribe(type => {
          this.schema.set([...type.fields].sort((a, b) => a.displayOrder - b.displayOrder));
        });
        this.loading.set(false);
      },
      error: () => { this.router.navigate(['/assets']); }
    });
  }

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
    return `badge ${map[s] ?? ''}`;
  }

  getAttrDisplay(field: AssetTypeFieldDto): string {
    const val = this.asset()?.attributes[field.name];
    if (val == null || val === '') return '—';
    if (field.dataType === FieldDataType.Boolean) return val ? 'Ναι' : 'Όχι';
    if (field.dataType === FieldDataType.Date || field.dataType === FieldDataType.DateTime) {
      const d = new Date(val as string);
      return field.dataType === FieldDataType.Date
        ? d.toLocaleDateString('el-GR')
        : d.toLocaleString('el-GR');
    }
    if (field.options.length > 0) {
      return field.options.find(o => o.value === String(val))?.label ?? String(val);
    }
    return String(val);
  }

  
}
