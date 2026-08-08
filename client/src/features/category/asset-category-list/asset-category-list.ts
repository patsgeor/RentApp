import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AssetService } from '../../../core/services/asset-service';
import { AssetTypeDto } from '../../../types/asset';

@Component({
  selector: 'app-asset-category-list',
  imports: [RouterLink, ReactiveFormsModule],
  templateUrl: './asset-category-list.html',
})
export class AssetCategoryList implements OnInit {
  private service = inject(AssetService);
  private fb      = inject(FormBuilder);

  types      = signal<AssetTypeDto[]>([]);
  loading    = signal(true);
  saving     = signal(false);
  deletingId = signal<string | null>(null);
  error      = signal<string | null>(null);
  showAddForm = signal(false);
  editingId  = signal<string | null>(null);

  form = this.fb.group({
    name:        ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(250)],
  });

  editForm = this.fb.group({
    name:        ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(250)],
  });

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.service.getAllAssetTypes().subscribe({
      next: types => { this.types.set(types); this.loading.set(false); },
      error: ()   => { this.error.set('Σφάλμα φόρτωσης'); this.loading.set(false); }
    });
  }

  openAdd() {
    this.form.reset({ name: '', description: '' });
    this.editingId.set(null);
    this.showAddForm.set(true);
  }

  cancelAdd() { this.showAddForm.set(false); }

  save() {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.error.set(null);
    const v = this.form.value;
    this.service.createAssetType({ name: v.name!, description: v.description || undefined }).subscribe({
      next: () => { this.saving.set(false); this.showAddForm.set(false); this.form.reset(); this.load(); },
      error: err => { this.saving.set(false); this.error.set(err.error?.message ?? 'Σφάλμα αποθήκευσης'); }
    });
  }

  startEdit(t: AssetTypeDto) {
    this.editingId.set(t.id);
    this.showAddForm.set(false);
    this.editForm.setValue({ name: t.name, description: t.description ?? '' });
  }

  cancelEdit() { this.editingId.set(null); }

  saveEdit(id: string) {
    if (this.editForm.invalid) return;
    this.saving.set(true);
    this.error.set(null);
    const v = this.editForm.value;
    this.service.updateAssetType(id, { name: v.name!, description: v.description || undefined }).subscribe({
      next: updated => {
        this.types.update(list => list.map(t => t.id === id ? { ...t, name: updated.name, description: updated.description } : t));
        this.editingId.set(null);
        this.saving.set(false);
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.message ?? 'Σφάλμα αποθήκευσης'); }
    });
  }

  confirmDelete(id: string, assetCount: number) {
    if (assetCount > 0) return;
    this.deletingId.set(id);
    this.error.set(null);
    this.service.deleteAssetType(id).subscribe({
      next: ()    => { this.types.update(list => list.filter(t => t.id !== id)); this.deletingId.set(null); },
      error: err  => { this.deletingId.set(null); this.error.set(err.error?.message ?? 'Σφάλμα διαγραφής'); }
    });
  }
}
