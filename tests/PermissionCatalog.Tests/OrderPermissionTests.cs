using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal static class OrderPermissionTests
{
    public static async Task RunAsync()
    {
        var permissions = new TestPermissions();
        var service = new OrderService(null!, null!, null!, null!, new AuthorizationProbeFactory(), permissions);
        for (var grants = 0; grants < 4; grants++)
        {
            permissions.Keys.Clear();
            permissions.Keys.Add(PermissionCatalog.Orders);
            if ((grants & 1) != 0) permissions.Keys.Add(PermissionCatalog.OrdersBarter);
            if ((grants & 2) != 0) permissions.Keys.Add(PermissionCatalog.OrdersDirectFromStock);
            for (var options = 0; options < 4; options++)
            {
                var order = new OrderEntity { IsBarter = (options & 1) != 0, DirectFromStock = (options & 2) != 0 };
                var allowed = (options & grants) == options;
                try
                {
                    await service.AddOrderAsync(order);
                    throw new InvalidOperationException("Expected authorization rejection or database boundary.");
                }
                catch (UnauthorizedAccessException) when (!allowed) { }
                catch (DatabaseBoundaryReachedException) when (allowed) { }
            }
        }
        // The same service instance must enforce a revocation, not retain constructor-time grants.
        permissions.Keys.Clear();
        try
        {
            await service.AddOrderAsync(new OrderEntity { IsBarter = true, DirectFromStock = true });
            throw new InvalidOperationException("Revoked order permissions were not enforced.");
        }
        catch (UnauthorizedAccessException) { }
        Console.WriteLine("PASS: separate barter/direct-stock permissions, all 16 grant/option combinations, and revocation before any database access");
    }

    private sealed class DatabaseBoundaryReachedException : Exception { }

    private sealed class AuthorizationProbeFactory : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => throw new DatabaseBoundaryReachedException();
    }
}
