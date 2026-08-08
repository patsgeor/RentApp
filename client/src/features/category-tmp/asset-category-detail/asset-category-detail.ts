import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AssetService } from '../../../core/services/asset-service';
import { AssetTypeDto, AssetTypeFieldDto, AssetTypeFieldOptionDto, FieldDataType } from '../../../types/asset';

const FIELD_NAME_PATTERN = /^[a-z][a-z0-9_]{1,49}$/;

@Component({
  selector: 'app-asset-category-detail',
  imports: [RouterLink, ReactiveFormsModule],
  templateUrl: './asset-category-detail.html',
})
export class AssetCategoryDetail implements OnInit {
  private route   = inject(ActivatedRoute);
  private service = inject(AssetService);
  private fb      = inject(FormBuilder);

  readonly FieldDataType = FieldDataType;

  typeId  = '';
  type    = signal<AssetTypeDto | null>(null);
  loading = signal(true);
  saving  = signal(false);
  error   = signal<string | null>(null);

  sortedFields = computed(() =>
    [...(this.type()?.fields ?? [])].sort((a, b) => a.displayOrder - b.displayOrder)
  );

  // ── Type header edit ─────────────────────────────────────────────────
  editingTypeInfo = signal(false);
  typeInfoForm = this.fb.group({
    name:        ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(250)],
  });

  // ── Fields ───────────────────────────────────────────────────────────
  showAddFieldForm   = signal(false);
  editingFieldId     = signal<string | null>(null);
  editingField       = signal<AssetTypeFieldDto | null>(null);
  deletingFieldId    = signal<string | null>(null);
  expandedFields     = signal<Set<string>>(new Set());

  // Track selected dataType for add-field form (to show/hide min/max)
  addFieldDataType = signal<FieldDataType>(FieldDataType.Text);

  addFieldForm = this.fb.group({
    name:         ['', [Validators.required, Validators.pattern(FIELD_NAME_PATTERN)]],
    label:        ['', [Validators.required, Validators.maxLength(100)]],
    dataType:     [FieldDataType.Text as number],
    isRequired:   [false],
    placeholder:  [''],
    displayOrder: [0],
    minValue:     [null as number | null],
    maxValue:     [null as number | null],
  });

  editFieldForm = this.fb.group({
    label:        ['', [Validators.required, Validators.maxLength(100)]],
    isRequired:   [false],
    placeholder:  [''],
    displayOrder: [0],
    minValue:     [null as number | null],
    maxValue:     [null as number | null],
  });

  // ── Options ──────────────────────────────────────────────────────────
  showAddOptionFieldId = signal<string | null>(null);
  editingOptionId      = signal<string | null>(null);
  deletingOptionId     = signal<string | null>(null);

  addOptionForm = this.fb.group({
    label:        ['', [Validators.required, Validators.maxLength(200)]],
    value:        ['', [Validators.required, Validators.maxLength(200)]],
    displayOrder: [0],
  });

  editOptionForm = this.fb.group({
    label:        ['', [Validators.required, Validators.maxLength(200)]],
    value:        ['', [Validators.required, Validators.maxLength(200)]],
    displayOrder: [0],
  });

  ngOnInit() {
    this.typeId = this.route.snapshot.paramMap.get('id')!;
    this.load();
  }

  load() {
    this.loading.set(true);
    this.service.getAssetTypeById(this.typeId).subscribe({
      next: t  => { this.type.set(t); this.loading.set(false); },
      error: () => { this.error.set('Σφάλμα φόρτωσης'); this.loading.set(false); }
    });
  }

  // ── Type info ─────────────────────────────────────────────────────────
  startEditType() {
    const t = this.type()!;
    this.typeInfoForm.setValue({ name: t.name, description: t.description ?? '' });
    this.editingTypeInfo.set(true);
  }

  cancelEditType() { this.editingTypeInfo.set(false); }

  saveType() {
    if (this.typeInfoForm.invalid) return;
    this.saving.set(true);
    this.error.set(null);
    const v = this.typeInfoForm.value;
    this.service.updateAssetType(this.typeId, { name: v.name!, description: v.description || undefined }).subscribe({
      next: updated => {
        this.type.update(t => t ? { ...t, name: updated.name, description: updated.description } : t);
        this.editingTypeInfo.set(false);
        this.saving.set(false);
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.message ?? 'Σφάλμα αποθήκευσης'); }
    });
  }

  // ── Fields ────────────────────────────────────────────────────────────
  toggleField(fieldId: string) {
    this.expandedFields.update(s => {
      const n = new Set(s);
      if (n.has(fieldId)) n.delete(fieldId); else n.add(fieldId);
      return n;
    });
  }

  isFieldExpanded(fieldId: string) { return this.expandedFields().has(fieldId); }

  openAddField() {
    this.addFieldForm.reset({ name: '', label: '', dataType: FieldDataType.Text, isRequired: false, placeholder: '', displayOrder: 0, minValue: null, maxValue: null });
    this.addFieldDataType.set(FieldDataType.Text);
    this.editingFieldId.set(null);
    this.showAddFieldForm.set(true);
  }

  cancelAddField() { this.showAddFieldForm.set(false); }

 
onAddFieldDataTypeChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value;
  this.addFieldDataType.set(Number(value) as FieldDataType);
}

  saveAddField() {
    if (this.addFieldForm.invalid) return;
    this.saving.set(true);
    this.error.set(null);
    const v = this.addFieldForm.value;
    this.service.addField(this.typeId, {
      name:         v.name!,
      label:        v.label!,
      dataType:     Number(v.dataType) as FieldDataType,
      isRequired:   v.isRequired ?? false,
      placeholder:  v.placeholder || undefined,
      displayOrder: v.displayOrder ?? 0,
      minValue:     v.minValue ?? undefined,
      maxValue:     v.maxValue ?? undefined,
    }).subscribe({
      next: newField => {
        this.type.update(t => t ? { ...t, fields: [...t.fields, newField] } : t);
        this.showAddFieldForm.set(false);
        this.saving.set(false);
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.message ?? 'Σφάλμα αποθήκευσης'); }
    });
  }

  startEditField(field: AssetTypeFieldDto) {
    this.editingField.set(field);
    this.editingFieldId.set(field.id);
    this.showAddFieldForm.set(false);
    this.showAddOptionFieldId.set(null);
    this.editFieldForm.setValue({
      label:        field.label,
      isRequired:   field.isRequired,
      placeholder:  field.placeholder ?? '',
      displayOrder: field.displayOrder,
      minValue:     field.minValue ?? null,
      maxValue:     field.maxValue ?? null,
    });
  }

  cancelEditField() { this.editingFieldId.set(null); this.editingField.set(null); }

  saveEditField(fieldId: string) {
    if (this.editFieldForm.invalid) return;
    this.saving.set(true);
    this.error.set(null);
    const v = this.editFieldForm.value;
    this.service.updateField(this.typeId, fieldId, {
      label:        v.label!,
      isRequired:   v.isRequired ?? false,
      placeholder:  v.placeholder || undefined,
      displayOrder: v.displayOrder ?? 0,
      minValue:     v.minValue ?? undefined,
      maxValue:     v.maxValue ?? undefined,
    }).subscribe({
      next: updated => {
        this.type.update(t => t ? {
          ...t, fields: t.fields.map(f => f.id === fieldId ? { ...f, ...updated } : f)
        } : t);
        this.editingFieldId.set(null);
        this.editingField.set(null);
        this.saving.set(false);
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.message ?? 'Σφάλμα αποθήκευσης'); }
    });
  }

  deleteField(fieldId: string) {
    this.deletingFieldId.set(fieldId);
    this.error.set(null);
    this.service.deleteField(this.typeId, fieldId).subscribe({
      next: ()   => { this.type.update(t => t ? { ...t, fields: t.fields.filter(f => f.id !== fieldId) } : t); this.deletingFieldId.set(null); },
      error: err => { this.deletingFieldId.set(null); this.error.set(err.error?.message ?? 'Σφάλμα διαγραφής'); }
    });
  }

  // ── Options ───────────────────────────────────────────────────────────
  openAddOption(fieldId: string) {
    this.addOptionForm.reset({ label: '', value: '', displayOrder: 0 });
    this.editingOptionId.set(null);
    this.showAddOptionFieldId.set(fieldId);
    this.expandedFields.update(s => { const n = new Set(s); n.add(fieldId); return n; });
  }

  cancelAddOption() { this.showAddOptionFieldId.set(null); }

  saveAddOption(fieldId: string) {
    if (this.addOptionForm.invalid) return;
    this.saving.set(true);
    this.error.set(null);
    const v = this.addOptionForm.value;
    this.service.addOption(this.typeId, fieldId, { label: v.label!, value: v.value!, displayOrder: v.displayOrder ?? 0 }).subscribe({
      next: newOpt => {
        this.type.update(t => t ? {
          ...t, fields: t.fields.map(f => f.id === fieldId ? { ...f, options: [...f.options, newOpt] } : f)
        } : t);
        this.showAddOptionFieldId.set(null);
        this.saving.set(false);
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.message ?? 'Σφάλμα αποθήκευσης'); }
    });
  }

  startEditOption(option: AssetTypeFieldOptionDto) {
    this.editingOptionId.set(option.id);
    this.showAddOptionFieldId.set(null);
    this.editOptionForm.setValue({ label: option.label, value: option.value, displayOrder: option.displayOrder });
  }

  cancelEditOption() { this.editingOptionId.set(null); }

  saveEditOption(fieldId: string, optionId: string) {
    if (this.editOptionForm.invalid) return;
    this.saving.set(true);
    this.error.set(null);
    const v = this.editOptionForm.value;
    this.service.updateOption(this.typeId, fieldId, optionId, { label: v.label!, value: v.value!, displayOrder: v.displayOrder ?? 0 }).subscribe({
      next: updated => {
        this.type.update(t => t ? {
          ...t, fields: t.fields.map(f => f.id === fieldId ? {
            ...f, options: f.options.map(o => o.id === optionId ? updated : o)
          } : f)
        } : t);
        this.editingOptionId.set(null);
        this.saving.set(false);
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.message ?? 'Σφάλμα αποθήκευσης'); }
    });
  }

  deleteOption(fieldId: string, optionId: string) {
    this.deletingOptionId.set(optionId);
    this.error.set(null);
    this.service.deleteOption(this.typeId, fieldId, optionId).subscribe({
      next: ()   => {
        this.type.update(t => t ? {
          ...t, fields: t.fields.map(f => f.id === fieldId ? { ...f, options: f.options.filter(o => o.id !== optionId) } : f)
        } : t);
        this.deletingOptionId.set(null);
      },
      error: err => { this.deletingOptionId.set(null); this.error.set(err.error?.message ?? 'Σφάλμα διαγραφής'); }
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────
  fieldDataTypeLabel(dt: FieldDataType): string {
    const map: Record<number, string> = {
      [FieldDataType.Text]:     'Κείμενο',
      [FieldDataType.Number]:   'Αριθμός',
      [FieldDataType.Boolean]:  'Ναι/Όχι',
      [FieldDataType.Date]:     'Ημερομηνία',
      [FieldDataType.DateTime]: 'Ημ/νία & Ώρα',
    };
    return map[dt] ?? '—';
  }

  fieldDataTypeBadgeClass(dt: FieldDataType): string {
    const map: Record<number, string> = {
      [FieldDataType.Text]:     'badge-ghost',
      [FieldDataType.Number]:   'badge-info',
      [FieldDataType.Boolean]:  'badge-warning',
      [FieldDataType.Date]:     'badge-success',
      [FieldDataType.DateTime]: 'badge-success',
    };
    return `badge badge-sm ${map[dt] ?? 'badge-ghost'}`;
  }
}
