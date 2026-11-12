using Infrastructure.Entities;

namespace Infrastructure.Dtos;

public sealed record RetailManagerState(int UserId, string? OwnManagerId, bool CanViewAllManagers,
    bool CanViewAllCustomers, IReadOnlyList<SalesManager> Managers, IReadOnlyList<CustomerEntity> Customers);
