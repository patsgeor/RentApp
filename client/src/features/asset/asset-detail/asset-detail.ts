import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe, CurrencyPipe } from '@angular/common';
import { AssetService } from '../../../core/services/asset-service';
import {
  AssetDto, AssetStatus, AcquisitionType, AssetTypeFieldDto,
  CostAssetHistDto, CostAssetHistCreateDto, FieldDataType
} from '../../../types/asset';

@Component({
  selector: 'app-asset-detail',
  imports: [RouterLink, ReactiveFormsModule, DatePipe, CurrencyPipe],
  templateUrl: './asset-detail.html',
})
export class AssetDetail implements OnInit {
  private fb      = inject(FormBuilder);
  private route   = inject(ActivatedRoute);
  private router  = inject(Router);
  private service = inject(AssetService);

  readonly AssetStatus     = AssetStatus;
  readonly AcquisitionType = AcquisitionType;
  readonly FieldDataType   = FieldDataType;

  asset              = signal<AssetDto | null>(null);
  schema             = signal<AssetTypeFieldDto[]>([]);
  maintenanceHistory = signal<CostAssetHistDto[]>([]);
  loading            = signal(true);
  showAddMaint       = signal(false);
  maintSaving        = signal(false);
  maintError         = signal('');
  private assetId!: string;

  maintForm = this.fb.group({
    date:         [new Date().toISOString().substring(0, 10), Validators.required],
    description:  ['', [Validators.required, Validators.maxLength(250)]],
    cost:         [0 as number, [Validators.required, Validators.min(0)]],
    maintainedBy: ['', Validators.maxLength(100)],
  });

  get mf() { return this.maintForm.controls; }

  ngOnInit() {
    this.assetId = this.route.snapshot.paramMap.get('id')!;
    this.service.getById(this.assetId).subscribe({
      next: (asset) => {
        this.asset.set(asset);
        this.service.getAssetTypeById(asset.assetTypeId).subscribe(type => {
          this.schema.set([...type.fields].sort((a, b) => a.displayOrder - b.displayOrder));
        });
        this.loading.set(false);
      },
      error: () => { this.router.navigate(['/assets']); }
    });
    this.service.getMaintenanceHistory(this.assetId).subscribe(h => this.maintenanceHistory.set(h));
  }

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

  saveMaintenance() {
    if (this.maintForm.invalid) { this.maintForm.markAllAsTouched(); return; }
    this.maintSaving.set(true);
    this.maintError.set('');
    const dto: CostAssetHistCreateDto = {
      date: new Date(this.mf['date'].value!).toISOString(),
      description: this.mf['description'].value!,
      cost: +this.mf['cost'].value!,
      maintainedBy: this.mf['maintainedBy'].value || undefined,
    };
    this.service.addMaintenanceRecord(this.assetId, dto).subscribe({
      next: (record) => {
        this.maintenanceHistory.update(h => [record, ...h]);
        this.showAddMaint.set(false);
        this.maintForm.reset({ date: new Date().toISOString().substring(0, 10), cost: 0 });
        this.maintSaving.set(false);
      },
      error: () => { this.maintError.set('Σφάλμα αποθήκευσης.'); this.maintSaving.set(false); }
    });
  }
}
