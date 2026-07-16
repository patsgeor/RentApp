import { Component, inject, OnInit, signal } from '@angular/core';
import { CustomerService } from '../../../core/services/customer-service';
import { CustomerDto, CustomerStatsDto } from '../../../types/customers';
import { CustomerStats } from "../customer-stats/customer-stats";
import { CustomersParams, PaginatedResult } from '../../../types/pagination';
import { RouterLink } from '@angular/router';
import { CustomerTable } from "../customer-table/customer-table";

@Component({
  selector: 'app-customer-list',
  imports: [RouterLink, CustomerStats, CustomerStats, CustomerTable], 
  templateUrl: './customer-list.html',
  styleUrl: './customer-list.css',
})
export class CustomerList implements OnInit {
 private customerService = inject(CustomerService);

  result       = signal<PaginatedResult<CustomerDto> | null>(null);
  stats        = signal<CustomerStatsDto | null>(null);
  loading      = signal(true);
  error        = signal('');
  activeFilter = signal('active');
  params       = new CustomersParams();

  private searchTimer?: ReturnType<typeof setTimeout>;

  ngOnInit() {
    // const saved = localStorage.getItem('customerFilters');
    // if (saved) this.params = { ...this.params, ...JSON.parse(saved) };
    this.loadStats();
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set('');
    this.customerService.getCustomers(this.params).subscribe({
      next: (res) => { this.result.set(res); this.loading.set(false); },
      error: () => { this.error.set('Σφάλμα φόρτωσης πελατών.'); this.loading.set(false); }
    });
  }

  loadStats() {
    this.customerService.getStats().subscribe({
      next: (s) => this.stats.set(s),
      error: () => {} // τα στατιστικά δεν μπλοκάρουν τη λίστα
    });
  }

  
  onFilterChange(filter: string) {
    this.activeFilter.set(filter);
    // 'new' maps to active customers (no special backend filter, just visual)
    this.params.showDeleted = filter === 'deleted' ? 'deleted'
                            : filter === 'all'     ? 'all'
                            : 'active';
    this.params.pageNumber = 1;
    this.load();
  }

  onPageChange(e: { pageNumber: number; pageSize: number }) {
    this.params.pageNumber = e.pageNumber;
    this.params.pageSize = e.pageSize;
    this.load();
  }

  onRestored() {
    this.load();
    this.loadStats();
  }


  onSearch(term: string) {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.params.searchTerm = term;
      this.params.pageNumber = 1;
      this.load();
    }, 350);
  }

  onDeleted() {
    this.load();
    this.loadStats();
  }
}