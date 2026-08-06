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
  // name_asc | name_desc | afm_asc | afm_desc | address_asc | address_desc | date_asc | date_desc
  orderBy = 'name_asc';
  searchTerm = '';
  showDeleted = 'active'; // 'active' | 'deleted' | 'all'
  newThisMonth = false;
}