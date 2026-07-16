export type PaginationMetadata ={
    currentPage :number ;
    pageSize :number ;
    totalCount :number;
    totalPages: number;
}

export type PaginatedResult<T> ={
    metadata: PaginationMetadata;
    items:T[];
}


export class CustomersParams {
  pageNumber = 1;
  pageSize = 10;
  orderBy = 'name';
  searchTerm = '';
  showDeleted = 'active'; // 'active' | 'deleted' | 'all'
}