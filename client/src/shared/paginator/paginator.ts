import { Component, computed, input, model, output } from '@angular/core';

@Component({
  selector: 'app-paginator',
  imports: [],
  templateUrl: './paginator.html',
  styleUrl: './paginator.css',
})
export class Paginator {
  pageSizeOptions = input<number[]>([5, 10, 20, 50]);
  pageNumber = model(1);
  pageSize = model(10);
  totalPages = input(1);
  totalCount = input(0);

  firstItemIndex = computed(() =>
    this.totalCount() === 0 ? 0 : (this.pageNumber() - 1) * this.pageSize() + 1
  );
  lastItemIndex = computed(() =>
    Math.min(this.totalCount(), this.pageSize() * this.pageNumber())
  );

  pageChange = output<{ pageNumber: number; pageSize: number }>();

  onPageChange(pageNumber?: number, pageSize?: EventTarget | null) {
    if (pageNumber) this.pageNumber.set(pageNumber);

    if (pageSize) {
      const size = Number((pageSize as HTMLSelectElement).value);
      this.pageSize.set(size);
      // Όταν αλλάζει το μέγεθος σελίδας, επιστρέφουμε στην 1η σελίδα
      this.pageNumber.set(1);
    }

    this.pageChange.emit({
      pageNumber: this.pageNumber(),
      pageSize: this.pageSize(),
    });
  }
}