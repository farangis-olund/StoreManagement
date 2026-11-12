using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class UserService(IDbContextFactory<DatabaseContext> factory)
{
    private static IQueryable<UserEntity> Users(DatabaseContext db) => db.Users.AsNoTracking()
        .Include(u => u.UserRoles).ThenInclude(r => r.Role).Include(u => u.SalesManager);

    public virtual async Task<List<UserEntity>> GetAllAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Users(db).OrderBy(u => u.UserName).ToListAsync();
    }

    public virtual async Task<List<RoleEntity>> GetRolesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Roles.AsNoTracking().OrderBy(r => r.RoleName).ToListAsync();
    }

    public virtual Task AddAsync(UserEntity user, int roleId) => SaveAsync(user, roleId, true);

    public virtual Task UpdateAsync(UserEntity user, int? newRoleId = null) => SaveAsync(user, newRoleId, false);

    private async Task SaveAsync(UserEntity input, int? roleId, bool create)
    {
        if (string.IsNullOrWhiteSpace(input.UserName) || string.IsNullOrWhiteSpace(input.FirstName) ||
            string.IsNullOrWhiteSpace(input.LastName) || string.IsNullOrWhiteSpace(input.Password))
            throw new ArgumentException("Заполните логин, имя, фамилию и пароль пользователя.");
        if (create && input.Id != 0 || !create && input.Id <= 0) throw new ArgumentException("Некорректный пользователь.");
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await LockProfilesAsync(db);
        var login = input.UserName.Trim();
        if (await db.Users.AnyAsync(u => u.UserName == login && u.Id != input.Id))
            throw new InvalidOperationException("Пользователь с таким логином уже существует.");
        if (create && !roleId.HasValue || roleId.HasValue && !await db.Roles.AnyAsync(r => r.Id == roleId.Value))
            throw new ArgumentException("Выберите существующую роль пользователя.");
        var saved = create ? new UserEntity() : await db.Users.SingleOrDefaultAsync(u => u.Id == input.Id)
            ?? throw new InvalidOperationException("Пользователь больше не существует. Обновите список.");
        saved.UserName = login;
        saved.FirstName = input.FirstName.Trim(); saved.LastName = input.LastName.Trim();
        saved.Email = input.Email; saved.PhoneNumber = input.PhoneNumber; saved.Address = input.Address;
        saved.Password = input.Password; saved.IsManager = input.IsManager;
        if (create) { db.Users.Add(saved); await db.SaveChangesAsync(); }
        await SynchronizeManagerAsync(db, saved);
        if (roleId.HasValue)
        {
            var links = await db.UserRoles.Where(r => r.UserId == saved.Id).ToListAsync();
            db.UserRoles.RemoveRange(links.Where(r => r.RoleId != roleId.Value));
            if (!links.Any(r => r.RoleId == roleId.Value)) db.UserRoles.Add(new() { UserId = saved.Id, RoleId = roleId.Value });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        input.Id = saved.Id;
    }

    private static async Task SynchronizeManagerAsync(DatabaseContext db, UserEntity user)
    {
        var manager = await db.SalesManagers.SingleOrDefaultAsync(m => m.UserId == user.Id);
        if (user.IsManager && manager == null)
        {
            var ids = await db.SalesManagers.Select(m => m.Id).ToListAsync();
            var next = ids.Where(id => id.StartsWith("M")).Select(id => long.TryParse(id.AsSpan(1), out var n) ? n : 0).DefaultIfEmpty().Max() + 1;
            manager = new SalesManagerEntity { Id = $"M{next}", FullName = user.FullName };
            db.SalesManagers.Add(manager);
            manager.UserId = user.Id;
        }
        if (manager != null)
        {
            manager.FullName = user.FullName;
            manager.Address = user.Address;
            manager.Contacts = user.PhoneNumber;
            manager.Active = user.IsManager;
        }
    }

    public virtual async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await LockProfilesAsync(db);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (user == null) return;
        if (await db.Orders.AnyAsync(o => o.UserId == id))
            throw new InvalidOperationException("Нельзя удалить пользователя, у которого есть заказы: история продаж должна сохраняться.");
        var manager = await db.SalesManagers.SingleOrDefaultAsync(m => m.UserId == id);
        if (manager != null) { manager.Active = false; manager.UserId = null; }
        db.Users.Remove(user);
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }

    public async Task<UserEntity?> GetByIdAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Users(db).SingleOrDefaultAsync(u => u.Id == id);
    }

    public async Task<UserEntity?> GetByUsernameAsync(string username)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Users(db).SingleOrDefaultAsync(u => u.UserName == username);
    }

    public async Task<List<string>> GetPermissionKeysAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.RolePermissions.Where(rp => db.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == rp.RoleId))
            .Select(rp => rp.Permission.Key).Distinct().ToListAsync();
    }

    public async Task<bool> ExistsAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AnyAsync(u => u.UserName == username.Trim());
    }

    private static Task LockProfilesAsync(DatabaseContext db) => db.Database.ExecuteSqlRawAsync("""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource=N'UserManagerProfiles', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
        IF @result < 0 THROW 51003, N'Не удалось сохранить пользователя. Повторите попытку.', 1;
        """);
}
