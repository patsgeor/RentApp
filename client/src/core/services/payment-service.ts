import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';

import { PaginatedResult } from '../../types/pagination';
import { ContractPaymentDto, ExpenseCreateDto, IncomeCreateDto, PaymentListItemDto } from '../../types/payment';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}payment`;

  getContracts(search?: string, status?: number, page = 1, pageSize = 10) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (status !== undefined && status !== null) params = params.set('status', status);
    return this.http.get<PaginatedResult<ContractPaymentDto>>(`${this.base}/contracts`, { params });
  }

  recordIncome(dto: IncomeCreateDto) {
    return this.http.post<PaymentListItemDto>(`${this.base}/income`, dto);
  }

  recordExpense(dto: ExpenseCreateDto, file?: File) {
    const form = new FormData();
    form.append('amount', dto.amount.toString());
    form.append('paymentDate', dto.paymentDate);
    form.append('paymentMethod', dto.paymentMethod.toString());
    form.append('description', dto.description);
    if (dto.notes) form.append('notes', dto.notes);
    dto.assetIds?.forEach(id => form.append('assetIds', id));
    if (file) form.append('file', file);
    return this.http.post<PaymentListItemDto>(`${this.base}/expenses`, form);
  }

  getIncome(page = 1, pageSize = 10, contractId?: string) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (contractId) params = params.set('contractId', contractId);
    return this.http.get<PaginatedResult<PaymentListItemDto>>(`${this.base}/income`, { params });
  }

  getExpenses(page = 1, pageSize = 10) {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PaginatedResult<PaymentListItemDto>>(`${this.base}/expenses`, { params });
  }

  deletePayment(id: string) {
    return this.http.delete(`${this.base}/${id}`);
  }
}
