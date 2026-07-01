import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  AssetDto, AssetCreateDto, AssetUpdateDto, AssetStatusUpdateDto,
  AssetLookupDto, AssetTypeLookupDto, AssetTypeDto,
  CostAssetHistDto, CostAssetHistCreateDto
} from '../../types/asset';
import { PaginatedResult } from '../../types/pagination';

@Injectable({ providedIn: 'root' })
export class AssetService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}asset`;
  private typeBase = `${environment.apiUrl}assettype`;

  getAssets(pageNumber: number, pageSize: number, searchTerm?: string, assetTypeId?: string, status?: number) {
    let params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    if (searchTerm) params = params.set('search', searchTerm);
    if (assetTypeId) params = params.set('assetTypeId', assetTypeId);
    if (status !== undefined && status !== null) params = params.set('status', status);
    return this.http.get<PaginatedResult<AssetDto>>(this.base, { params });
  }

  getById(id: string) { return this.http.get<AssetDto>(`${this.base}/${id}`); }

  create(dto: AssetCreateDto) { return this.http.post<AssetDto>(this.base, dto); }

  update(id: string, dto: AssetUpdateDto) { return this.http.put<AssetDto>(`${this.base}/${id}`, dto); }

  updateStatus(id: string, dto: AssetStatusUpdateDto) {
    return this.http.patch<AssetDto>(`${this.base}/${id}/status`, dto);
  }

  delete(id: string) { return this.http.delete(`${this.base}/${id}`); }

  getAssetTypes() { return this.http.get<AssetTypeLookupDto[]>(`${this.typeBase}/lookup`); }

  getAssetTypeById(id: string) { return this.http.get<AssetTypeDto>(`${this.typeBase}/${id}`); }

  getMaintenanceHistory(assetId: string) {
    return this.http.get<CostAssetHistDto[]>(`${this.base}/${assetId}/maintenance-history`);
  }

  addMaintenanceRecord(assetId: string, dto: CostAssetHistCreateDto) {
    return this.http.post<CostAssetHistDto>(`${this.base}/${assetId}/maintenance-history`, dto);
  }
}
