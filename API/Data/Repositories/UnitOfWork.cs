using System;
using API.Data.Contexts;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

 
public class UnitOfWork(
    AppDbContext context,
    UserManager<AppUser> userManager,
    IEmailService emailService,
    ITenantProvider tenantProvider
    ) : IUnitOfWork
{
    // Lazy-init: each repo is only constructed if a service actually asks for it,
    // but all repos share the SAME AppDbContext instance (scoped per-request),
    // so they all see each other's tracked changes before Complete() is called.
    private ICustomerRepository? _customerRepository;
    private IAssetRepository? _assetRepository;
    private IContractRepository? _contractRepository;
    private IMemberRepository? _memberRepository;
    
    public ICustomerRepository CustomerRepository =>
        _customerRepository ??= new CustomerRepository(context);
 
    public IAssetRepository AssetRepository =>
        _assetRepository ??= new AssetRepository(context, tenantProvider);
 
    public IContractRepository ContractRepository =>
        _contractRepository ??= new ContractRepository(context);
 
    public IMemberRepository MemberRepository =>
        _memberRepository ??= new MemberRepository(context, userManager, emailService, tenantProvider );
 
    public async Task<bool> Complete()
    {
        try
        {
            return await context.SaveChangesAsync()>0;
        }
        catch (DbUpdateException ex)
        {
            throw new Exception("An error occurred while saving changes", ex);
        }
    }
 
    public bool HasChanges()
    {
        return context.ChangeTracker.HasChanges();
    }
}