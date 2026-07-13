using System;
using static API.Entities.Enums;

namespace API.Helper;

public class DebtParams : PagingParams
{
    public int? Month { get; set; }
    public int? Year { get; set; }
    public InstallmentStatus? Status { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ContractId { get; set; }
}
