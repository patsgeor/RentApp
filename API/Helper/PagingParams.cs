using System;

namespace API.Helper;

public class PagingParams
{
    private const int MaxPageSize =50;
    private int _pageSize=5;
    public int PageNumber {get; set;}=1;


    public int PageSize {
        get => _pageSize ; 
        set => _pageSize=(value>MaxPageSize)? MaxPageSize:value;
    }

    public string? Search { get; set; }

    // name_asc | name_desc | cost_asc | cost_desc | date_asc | date_desc
    public string? SortBy { get; set; }
}

public class CustomerParams : PagingParams
{
    /// <summary>active | deleted | all</summary>
    public string ShowDeleted { get; set; } = "active";
}


