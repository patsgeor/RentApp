import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  TenantAdminDto, SubscriptionStatus, TenantUserDto,
  AuditLogDto, ErrorLogDto, PlatformSummaryDto
} from '../../types/tenant';
import { PlanType, UpgradeRequestDto, UpgradeRequestStatus } from '../../types/user';
import { PaginatedResult } from '../../types/pagination';

@Injectable({ providedIn: 'root' })
export class TenantService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}tenant`;

  getAll() {
    return this.http.get<TenantAdminDto[]>(this.base);
  }

  getUsers(tenantId: string) {
    return this.http.get<TenantUserDto[]>(`${this.base}/${tenantId}/users`);
  }

  getPlatformSummary() {
    return this.http.get<PlatformSummaryDto>(`${this.base}/platform-summary`);
  }

  getAuditLogs(filters: {
    tenantId?: string; action?: string; search?: string;
    page?: number; pageSize?: number;
  } = {}) {
    let params = new HttpParams()
      .set('page', filters.page ?? 1)
      .set('pageSize', filters.pageSize ?? 50);
    if (filters.tenantId) params = params.set('tenantId', filters.tenantId);
    if (filters.action)   params = params.set('action', filters.action);
    if (filters.search)   params = params.set('search', filters.search);
    return this.http.get<PaginatedResult<AuditLogDto>>(`${this.base}/audit-logs`, { params });
  }

  getErrorLogs(filters: {
    tenantId?: string; search?: string; page?: number; pageSize?: number;
  } = {}) {
    let params = new HttpParams()
      .set('page', filters.page ?? 1)
      .set('pageSize', filters.pageSize ?? 50);
    if (filters.tenantId) params = params.set('tenantId', filters.tenantId);
    if (filters.search)   params = params.set('search', filters.search);
    return this.http.get<PaginatedResult<ErrorLogDto>>(`${this.base}/error-logs`, { params });
  }

  updatePlan(id: string, planType: PlanType) {
    return this.http.patch<{ id: string; name: string; planType: PlanType }>(
      `${this.base}/${id}/plan`, { planType }
    );
  }

  updateStatus(id: string, subscriptionStatus: SubscriptionStatus) {
    return this.http.patch<{ id: string; name: string; subscriptionStatus: SubscriptionStatus }>(
      `${this.base}/${id}/status`, { subscriptionStatus }
    );
  }

  getUpgradeRequests(status?: UpgradeRequestStatus) {
    let params = new HttpParams();
    if (status !== undefined) params = params.set('status', status);
    return this.http.get<UpgradeRequestDto[]>(`${this.base}/upgrade-requests`, { params });
  }

  approveUpgradeRequest(id: string, reviewNote?: string) {
    return this.http.post<{ id: string; name: string; planType: PlanType }>(
      `${this.base}/upgrade-requests/${id}/approve`, { reviewNote }
    );
  }

  rejectUpgradeRequest(id: string, reviewNote?: string) {
    return this.http.post<{ id: string; status: UpgradeRequestStatus }>(
      `${this.base}/upgrade-requests/${id}/reject`, { reviewNote }
    );
  }
}
