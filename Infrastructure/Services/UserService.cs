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

        public async Task AddAsync(UserEntity user, string roleId)
        {
            user.Id = Guid.NewGuid().ToString();
            var role = await _context.Roles.FindAsync(roleId);
            if (role != null)
            {
                user.UserRoles.Add(new UserRoleEntity
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(UserEntity user, string? newRoleId = null)
        {
            var existing = await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (existing == null) return;

            existing.UserName = user.UserName;
            existing.FirstName = user.FirstName;
            existing.LastName = user.LastName;
            existing.Email = user.Email;
            existing.PhoneNumber = user.PhoneNumber;
            existing.Password = user.Password;

            if (!string.IsNullOrEmpty(newRoleId))
            {
                existing.UserRoles.Clear();
                existing.UserRoles.Add(new UserRoleEntity
                {
                    UserId = existing.Id,
                    RoleId = newRoleId
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(string id)
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
    }
}
