import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AssetService } from '../../../core/services/asset-service';
import { AssetDto,  AssetTypeLookupDto } from '../../../types/asset';
import { PaginatedResult } from '../../../types/pagination';
import { AssetTable } from '../asset-table/asset-table';

@Component({
  selector: 'app-asset-list',
  imports: [RouterLink, AssetTable],
  templateUrl: './asset-list.html',
})
export class AssetList implements OnInit {
  private service = inject(AssetService);

  result   = signal<PaginatedResult<AssetDto> | null>(null);
  assetTypes = signal<AssetTypeLookupDto[]>([]);
  loading  = signal(false);
  error    = signal('');

  private pageNumber   = 1;
  private pageSize     = 10;
  private searchTerm   = '';
  private assetTypeId  = '';
  private statusFilter: number | undefined = undefined;

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit() {
    this.service.getAssetTypes().subscribe(types => this.assetTypes.set(types));
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set('');
    this.service.getAssets(
      this.pageNumber, this.pageSize,
      this.searchTerm || undefined,
      this.assetTypeId || undefined,
      this.statusFilter
    ).subscribe({
      next: (r) => { this.result.set(r); this.loading.set(false); },
      error: () => { this.error.set('Σφάλμα φόρτωσης παγίων.'); this.loading.set(false); }
    });
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
    this.pageNumber = 1;
    this.load();
  }

  onStatusChange(val: string) {
    this.statusFilter = val === '' ? undefined : +val;
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
