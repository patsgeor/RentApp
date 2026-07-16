import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { AadeCompanyDto, ContactDto, CreateCustomerDto, CustomerDto, CustomerLookupDto, CustomerStatsDto } from '../../types/customers';
import { CustomersParams, PaginatedResult } from '../../types/pagination';

@Injectable({
  providedIn: 'root',
})
export class CustomerService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  getCustomers(customersParams: CustomersParams) {
    let params = new HttpParams();
    params = params.append('pageNumber', customersParams.pageNumber);
    params = params.append('pageSize', customersParams.pageSize);
    params = params.append('orderBy', customersParams.orderBy);
    if (customersParams.searchTerm) params = params.append('search', customersParams.searchTerm);
    if (customersParams.showDeleted) params = params.append('showDeleted', customersParams.showDeleted);

     return this.http.get<PaginatedResult<CustomerDto>>(this.baseUrl + 'customer', { params });
  }

  restore(id: string) {
    return this.http.post<CustomerDto>(`${this.baseUrl}customer/${id}/restore`, {});
  }


  getStats() { return this.http.get<CustomerStatsDto>(`${this.baseUrl}customer/stats`); }

  getById(id: string) { return this.http.get<CustomerDto>(`${this.baseUrl}customer/${id}`); }
  create(dto: CreateCustomerDto) { return this.http.post<CustomerDto>(`${this.baseUrl}customer/`, dto); }
  update(id: string, dto: CreateCustomerDto) { return this.http.put<CustomerDto>(`${this.baseUrl}customer/${id}`, dto); }
  delete(id: string) { return this.http.delete(`${this.baseUrl}customer/${id}`); }

   getLookup(search?: string) {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<CustomerLookupDto[]>(`${this.baseUrl}customer/lookup`, { params });
  }

  
  addContact(customerId: string, dto: object) {
    return this.http.post<ContactDto>(`${this.baseUrl}customer/${customerId}/contacts`, dto);
  }
  updateContact(customerId: string, contactId: string, dto: object) {
    return this.http.put<ContactDto>(`${this.baseUrl}customer/${customerId}/contacts/${contactId}`, dto);
  }
  deleteContact(customerId: string, contactId: string) {
    return this.http.delete(`${this.baseUrl}customer/${customerId}/contacts/${contactId}`);
  }

  getAadeCompany(afm: string) {
    return this.http.get<AadeCompanyDto>(`${this.baseUrl}aade/lookup/${afm}`);
  }
}
