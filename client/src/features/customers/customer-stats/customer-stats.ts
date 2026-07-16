import { Component, computed, input, output } from '@angular/core';
import { CustomerDto, CustomerStatsDto } from '../../../types/customers';

@Component({
  selector: 'app-customer-stats',
  imports: [],
  templateUrl: './customer-stats.html',
  styleUrl: './customer-stats.css',
})
export class CustomerStats {
  stats       = input<CustomerStatsDto | null>(null);
  activeFilter = input<string>('active');
  filterChange = output<string>();
}
