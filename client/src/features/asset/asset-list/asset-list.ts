import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AssetService } from '../../../core/services/asset-service';
import { AssetDto,  AssetTypeFieldDto,  AssetTypeLookupDto } from '../../../types/asset';
import { PaginatedResult } from '../../../types/pagination';
import { AssetCards } from '../asset-cards/asset-cards';

export interface ActiveChip {
  fieldName: string;
  fieldLabel: string;
  value: string;
  label: string;
}

@Component({
  selector: 'app-asset-list',
  imports: [RouterLink, AssetCards],
  templateUrl: './asset-list.html',
})
export class AssetList implements OnInit {
  private service = inject(AssetService);

  result      = signal<PaginatedResult<AssetDto> | null>(null);
  assetTypes  = signal<AssetTypeLookupDto[]>([]);
  schemaFields= signal<AssetTypeFieldDto[]>([]);  // filterable fields (with options) for selected type
  activeChips = signal<ActiveChip[]>([]);
  loading     = signal(false);
  error       = signal('');

  private pageNumber   = 1;
  private pageSize     = 12;
  private searchTerm   = '';
  private assetTypeId  = '';
  private statusFilter: number | undefined = undefined;
  private sortBy       = 'date_desc';
   availableFrom  = '';
   availableTo    = '';

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit() {
    this.service.getAssetTypes().subscribe(types => this.assetTypes.set(types));
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set('');
    
    const chips = this.activeChips();
    if (chips.length > 0 && this.assetTypeId) {
      // use search endpoint for attribute filters
      const filters = chips.map(c => ({ fieldName: c.fieldName, equals: c.value }));
      this.service.searchAssets({
        assetTypeId: this.assetTypeId,
        search: this.searchTerm || undefined,
        pageNumber: this.pageNumber,
        pageSize: this.pageSize,
        sortBy: this.sortBy,
        filters
      }).subscribe({
        next: (r) => { this.result.set(r); this.loading.set(false); },
        error: () => { this.error.set('Σφάλμα φόρτωσης παγίων.'); this.loading.set(false); }
      });
    } else {
      this.service.getAssets(
        this.pageNumber, this.pageSize,
        this.searchTerm || undefined,
        this.assetTypeId || undefined,
        this.statusFilter,
        this.sortBy,
        this.availableFrom || undefined,
        this.availableTo || undefined
      ).subscribe({
        next: (r) => { this.result.set(r); this.loading.set(false); },
        error: () => { this.error.set('Σφάλμα φόρτωσης παγίων.'); this.loading.set(false); }
      });
    }
  }

  onSearch(term: string) {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.searchTerm = term;
      this.pageNumber = 1;
      this.load();
    }, 350);
  }

onAssetTypeChange(id: string) {
    this.assetTypeId = id;
    this.activeChips.set([]);
    this.schemaFields.set([]);
    this.pageNumber = 1;

    if (id) {
      this.service.getAssetTypeById(id).subscribe(type => {
        // Only show fields that have option lists (dropdown fields = filterable)
        this.schemaFields.set(type.fields.filter(f => f.options.length > 0));
      });
    }
    this.load();
  }

  onSortChange(val: string) {
    this.sortBy = val;
    this.pageNumber = 1;
    this.load();
  }

  addChip(field: AssetTypeFieldDto, optionValue: string) {
    const opt = field.options.find(o => o.value === optionValue);
    if (!opt) return;

    const existing = this.activeChips();
    // Replace existing filter for same field (one value per field)
    const updated = existing.filter(c => c.fieldName !== field.name);
    updated.push({ fieldName: field.name, fieldLabel: field.label, value: optionValue, label: opt.label });
    this.activeChips.set(updated);
    this.pageNumber = 1;
    this.load();
  }

  removeChip(fieldName: string) {
    this.activeChips.update(chips => chips.filter(c => c.fieldName !== fieldName));
    this.pageNumber = 1;
    this.load();
  }


  onStatusChange(val: string) {
    this.statusFilter = val === '' ? undefined : +val;
    this.pageNumber = 1;
    this.load();
  }

  onAvailableFromChange(val: string) {
    this.availableFrom = val;
    this.pageNumber = 1;
    this.load();
  }

  onAvailableToChange(val: string) {
    this.availableTo = val;
    this.pageNumber = 1;
    this.load();
  }

  clearDateFilter() {
    this.availableFrom = '';
    this.availableTo = '';
    this.pageNumber = 1;
    this.load();
  }

  onPageChange(e: { pageNumber: number; pageSize: number }) {
    this.pageNumber = e.pageNumber;
    this.pageSize   = e.pageSize;
    this.load();
  }

  onDeleted() { this.load(); }
}
