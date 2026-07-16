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

  pageChange = output<{ pageNumber: number; pageSize: number }>();
  searchChange = output<string>();
  deleted      = output<string>();
  restored     = output<string>();

  private router = inject(Router);
  private customerService = inject(CustomerService);

  onSearch(value: string) { this.searchChange.emit(value); }

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