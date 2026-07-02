using System;

namespace API.Interfaces;

public interface IUnitOfWork
{
    ICustomerRepository CustomerRepository { get; }
    IAssetRepository AssetRepository { get; }
    IContractRepository ContractRepository { get; }
    IMemberRepository MemberRepository { get; }
    IPaymentRepository PaymentRepository { get; }
 
    Task<bool> Complete();
    bool HasChanges();
}