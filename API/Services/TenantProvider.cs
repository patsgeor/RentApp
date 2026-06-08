using System;
using API.Interfaces;

namespace API.Services;

public class TenantProvider: ITenantProvider
{
    public Guid TenantId { get; set; }
}