import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AssetService } from '../../../core/services/asset-service';
import {
  AcquisitionType, AssetCreateDto, AssetTypeFieldDto,
  AssetTypeLookupDto, AssetUpdateDto, FieldDataType
} from '../../../types/asset';

@Component({
  selector: 'app-asset-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './asset-form.html',
  styleUrl: './asset-form.css',
})


export class AssetForm implements OnInit {
  private fb      = inject(FormBuilder);
  private router  = inject(Router);
  private route   = inject(ActivatedRoute);
  private service = inject(AssetService);

  readonly AcquisitionType = AcquisitionType;
  readonly FieldDataType   = FieldDataType;

  isEdit        = signal(false);
  loading       = signal(false);
  schemaLoading = signal(false);
  errorMsg      = signal('');
  private assetId: string | null = null;

  assetTypes = signal<AssetTypeLookupDto[]>([]);
  schema     = signal<AssetTypeFieldDto[]>([]);

  form = this.fb.group({
    assetTypeId:      ['', Validators.required],
    name:             ['', [Validators.required, Validators.maxLength(150)]],
    notes:            ['', Validators.maxLength(500)],
    acquisitionType:  [AcquisitionType.Purchase as AcquisitionType, Validators.required],
    acquisitionCost:  [0 as number, [Validators.required, Validators.min(0)]],
    monthlyLeaseCost: [null as number | null],
    attributes:       this.fb.group({}),
  });

  get f()     { return this.form.controls; }
  get attrs() { return this.form.get('attributes') as FormGroup; }
  get isLeasing() { return +this.f['acquisitionType'].value! === AcquisitionType.Leasing; }

  ngOnInit() {
    this.service.getAssetTypes().subscribe(types => this.assetTypes.set(types));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.assetId = id;
      this.service.getById(id).subscribe(asset => {
        this.f['assetTypeId'].setValue(asset.assetTypeId);
        this.f['assetTypeId'].disable();
        this.f['name'].setValue(asset.name);
        this.f['notes'].setValue(asset.notes ?? '');
        this.f['acquisitionType'].setValue(asset.acquisitionType);
        this.f['acquisitionCost'].setValue(asset.acquisitionCost);
        this.f['monthlyLeaseCost'].setValue(asset.monthlyLeaseCost ?? null);
        this.loadSchema(asset.assetTypeId, asset.attributes);
      });
    } else {
      this.f['assetTypeId'].valueChanges.subscribe(typeId => {
        if (typeId) this.loadSchema(typeId);
      });
    }
  }

  private loadSchema(typeId: string, existingValues?: Record<string, unknown>) {
    this.schemaLoading.set(true);
    this.service.getAssetTypeById(typeId).subscribe(type => {
      const fields = [...type.fields].sort((a, b) => a.displayOrder - b.displayOrder);
      this.buildAttributeControls(fields, existingValues);
      this.schema.set(fields);
      this.schemaLoading.set(false);
    });
  }

  private buildAttributeControls(fields: AssetTypeFieldDto[], existingValues?: Record<string, unknown>) {
    const group = this.attrs;
    Object.keys(group.controls).forEach(k => group.removeControl(k));
    for (const field of fields) {
      const rawValue  = existingValues?.[field.name] ?? null;
      const formValue = this.toFormValue(field, rawValue);
      const validators: ValidatorFn[] = [];
      if (field.isRequired) validators.push(Validators.required);
      if (field.dataType === FieldDataType.Number) {
        if (field.minValue != null) validators.push(Validators.min(field.minValue));
        if (field.maxValue != null) validators.push(Validators.max(field.maxValue));
      }
      if (field.validationRegex) validators.push(Validators.pattern(field.validationRegex));
      group.addControl(field.name, this.fb.control(formValue, validators));
    }
  }

  private toFormValue(field: AssetTypeFieldDto, raw: unknown): unknown {
    if (raw == null) return null;
    if (field.dataType === FieldDataType.Date)
      return new Date(raw as string).toISOString().substring(0, 10);
    if (field.dataType === FieldDataType.DateTime)
      return new Date(raw as string).toISOString().substring(0, 16);
    return raw;
  }

  private buildAttributes(): Record<string, unknown> {
    const result: Record<string, unknown> = {};
    for (const field of this.schema()) {
      const val = this.attrs.get(field.name)?.value;
      if (val == null || val === '') continue;
      switch (field.dataType) {
        case FieldDataType.Number:
          result[field.name] = Number(val); break;
        case FieldDataType.Date:
        case FieldDataType.DateTime:
          result[field.name] = new Date(val as string).toISOString(); break;
        default:
          result[field.name] = val;
      }
    }
    return result;
  }

  onSubmit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    this.errorMsg.set('');

    const attributes = this.buildAttributes();
    const raw = this.form.getRawValue();

    const req$ = this.isEdit()
      ? this.service.update(this.assetId!, {
          name: raw.name!,
          notes: raw.notes || undefined,
          acquisitionType: +raw.acquisitionType! as AcquisitionType,
          acquisitionCost: +raw.acquisitionCost!,
          monthlyLeaseCost: raw.monthlyLeaseCost != null ? +raw.monthlyLeaseCost : undefined,
          attributes,
        } as AssetUpdateDto)
      : this.service.create({
          assetTypeId: raw.assetTypeId!,
          name: raw.name!,
          notes: raw.notes || undefined,
          acquisitionType: +raw.acquisitionType! as AcquisitionType,
          acquisitionCost: +raw.acquisitionCost!,
          monthlyLeaseCost: raw.monthlyLeaseCost != null ? +raw.monthlyLeaseCost : undefined,
          attributes,
        } as AssetCreateDto);

    req$.subscribe({
      next: (saved) => this.router.navigate(['/assets', saved.id]),
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Σφάλμα αποθήκευσης.');
        this.loading.set(false);
      }
    });
  }
}