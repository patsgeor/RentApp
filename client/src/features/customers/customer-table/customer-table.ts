import { Component, computed, inject, input, output, signal } from '@angular/core';
import { CustomerDto } from '../../../types/customers';
import { Router } from '@angular/router';
import { CustomerService } from '../../../core/services/customer-service';
import { DatePipe } from '@angular/common';
import { PaginationMetadata } from '../../../types/pagination';
import { Paginator } from '../../../shared/paginator/paginator';

@Component({
  selector: 'app-customer-table',
  imports: [DatePipe, Paginator],
  templateUrl: './customer-table.html',
  styleUrl: './customer-table.css',
})
export class CustomerTable {
  items      = input.required<CustomerDto[]>();
  pagination = input<PaginationMetadata | null>(null);
  orderBy    = input<string>('name_asc');

  pageChange = output<{ pageNumber: number; pageSize: number }>();
  searchChange = output<string>();
  sortChange   = output<string>();
  deleted      = output<string>();
  restored     = output<string>();

  private router = inject(Router);
  private customerService = inject(CustomerService);

  onSearch(value: string) { this.searchChange.emit(value); }

  /** Πρώτο κλικ → αύξουσα, δεύτερο στην ίδια στήλη → φθίνουσα. */
  toggleSort(field: string) {
    const current = this.orderBy();
    this.sortChange.emit(current === `${field}_asc` ? `${field}_desc` : `${field}_asc`);
  }

  sortIcon(field: string): string {
    const current = this.orderBy();
    if (current === `${field}_asc`)  return '▲';
    if (current === `${field}_desc`) return '▼';
    return '';
  }

  isSorted(field: string): boolean {
    return this.orderBy().startsWith(`${field}_`);
  }

  viewHistory(id: string) { this.router.navigate(['/customer', id]); }
  
  edit(id: string, e: Event) { e.stopPropagation(); this.router.navigate(['/customer', id, 'edit']); }

  delete(id: string, e: Event) {
    e.stopPropagation();
    if (confirm('Διαγραφή πελάτη;')) {
      this.customerService.delete(id).subscribe({ next: () => this.deleted.emit(id) });
    }
  }

  restore(id: string, e: Event) {
    e.stopPropagation();
    if (confirm('Ενεργοποίηση πελάτη;')) {
      this.customerService.restore(id).subscribe({ next: () => this.restored.emit(id) });
    }
  }

}