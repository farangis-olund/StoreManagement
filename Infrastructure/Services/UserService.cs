using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class UserService
    {
        private readonly DatabaseContext _context;

        public UserService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<List<UserEntity>> GetAllAsync()
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .ToListAsync();
        }

        public async Task<List<RoleEntity>> GetRolesAsync()
        {
            return await _context.Roles.ToListAsync();
        }

        public async Task AddAsync(UserEntity user, int roleId)
        {
            
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var role = await _context.Roles.FindAsync(roleId);
            if (role != null)
            {
                var userRole = new UserRoleEntity
                {
                    UserId = user.Id,
                    RoleId = role.Id
                };

                _context.UserRoles.Add(userRole);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateAsync(UserEntity user, int? newRoleId = null)
        {
            var existing = await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (existing == null)
                return;

            existing.UserName = user.UserName;
            existing.FirstName = user.FirstName;
            existing.LastName = user.LastName;
            existing.Email = user.Email;
            existing.PhoneNumber = user.PhoneNumber;
            existing.Password = user.Password; // (optional: update only if changed)

            // Update the role only if newRoleId has a value
            if (newRoleId.HasValue)
            {
                existing.UserRoles.Clear();
                existing.UserRoles.Add(new UserRoleEntity
                {
                    UserId = existing.Id,
                    RoleId = newRoleId.Value
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<UserEntity?> GetByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<UserEntity?> GetByUsernameAsync(string username)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserName == username);
        }

        public async Task<List<string>> GetPermissionKeysAsync(int userId)
        {
            // Load user with roles
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return new List<string>();

            // Extract role IDs
            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

            // Load permissions for those roles
            var permissionKeys = await _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission.Key)
                .Distinct()
                .ToListAsync();

            return permissionKeys;
        }

        public async Task<bool> ExistsAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            return await _context.Users
                .AnyAsync(u => u.UserName == username);
        }

    }
}
