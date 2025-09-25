using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
    public class OrganizationInfoService
    {
        private readonly IDbContextFactory<DatabaseContext> _dbFactory;

        public OrganizationInfoService(IDbContextFactory<DatabaseContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        // Get the first (or only) organization record
        public async Task<OrganizationInfoEntity?> GetAsync()
        {
            using var db = _dbFactory.CreateDbContext();
            return await db.OrganizationInfo.FirstOrDefaultAsync();
        }

        // Add new organization info
        public async Task<OrganizationInfoEntity> AddAsync(OrganizationInfoEntity org)
        {
            using var db = _dbFactory.CreateDbContext();
            db.OrganizationInfo.Add(org);
            await db.SaveChangesAsync();
            return org;
        }

        // Update existing organization info
        public async Task UpdateAsync(OrganizationInfoEntity org)
        {
            using var db = _dbFactory.CreateDbContext();
            db.OrganizationInfo.Update(org);
            await db.SaveChangesAsync();
        }

        // Delete by OrganizationCode
        public async Task DeleteAsync(string id)
        {
            using var db = _dbFactory.CreateDbContext();
            var entity = await db.OrganizationInfo
                                 .FirstOrDefaultAsync(x => x.OrganizationCode == id);
            if (entity != null)
            {
                db.OrganizationInfo.Remove(entity);
                await db.SaveChangesAsync();
            }
        }

        public async Task<string?> GetShopDisplayAsync()
        {
            using var db = _dbFactory.CreateDbContext();
            var org = await db.OrganizationInfo.FirstOrDefaultAsync();
            if (org == null) return null;

            return $"{org.OrganizationCode} | {org.Name}";
        }
    }
}
