import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  InstallmentDto, DebtFilterParams,
  ScheduleInstallmentItem, DebtStatsDto
} from '../../types/installment';
import { PaginatedResult } from '../../types/pagination';

@Injectable({ providedIn: 'root' })
export class InstallmentService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}installment`;

  getByContract(contractId: string) {
    return this.http.get<InstallmentDto[]>(`${this.base}/contract/${contractId}`);
  }

  getDebts(f: DebtFilterParams) {
    let params = new HttpParams()
      .set('pageNumber', f.pageNumber ?? 1)
      .set('pageSize',   f.pageSize   ?? 20);
    if (f.month      != null) params = params.set('month',      f.month);
    if (f.year       != null) params = params.set('year',       f.year);
    if (f.status     != null) params = params.set('status',     f.status);
    if (f.customerId)         params = params.set('customerId', f.customerId);
    if (f.contractId)         params = params.set('contractId', f.contractId);
    if (f.search)             params = params.set('search',     f.search);
    return this.http.get<PaginatedResult<InstallmentDto>>(`${this.base}/debts`, { params });
  }

  getStats(month?: number, year?: number) {
    let params = new HttpParams();
    if (month != null) params = params.set('month', month);
    if (year  != null) params = params.set('year',  year);
    return this.http.get<DebtStatsDto>(`${this.base}/stats`, { params });
  }

  generate(contractId: string) {
    return this.http.post<{ message: string }>(`${this.base}/generate/${contractId}`, {});
  }

  updateSchedule(contractId: string, schedule: ScheduleInstallmentItem[]) {
    return this.http.put<{ message: string }>(
      `${this.base}/contract/${contractId}/schedule`, schedule
    );
  }

  notifyEmail(installmentId: string) {
    return this.http.post<{ message: string }>(`${this.base}/${installmentId}/notify-email`, {});
  }

  cancel(installmentId: string) {
    return this.http.delete(`${this.base}/${installmentId}/cancel`);
  }
}