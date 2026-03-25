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

            // INSERT if code is missing or record does not exist
            bool isNew = string.IsNullOrWhiteSpace(org.OrganizationCode)
                         || !await db.OrganizationInfo.AnyAsync(o => o.OrganizationCode == org.OrganizationCode);

            if (isNew)
            {
                await db.OrganizationInfo.AddAsync(org);
            }
            else
            {
                // UPDATE existing record
                var existing = await db.OrganizationInfo
                    .FirstOrDefaultAsync(o => o.OrganizationCode == org.OrganizationCode);

                if (existing != null)
                {
                    db.Entry(existing).CurrentValues.SetValues(org);
                }
            }

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
