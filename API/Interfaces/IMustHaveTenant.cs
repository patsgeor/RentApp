using System;

namespace API.Interfaces;


public interface IMustHaveTenant
{
    Guid TenantId { get; set; }
}
