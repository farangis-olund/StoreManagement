using Infrastructure.Entities;
using Infrastructure.Repositories;

namespace Infrastructure.Services
{
    public class StoreService
    {
        private readonly StoreRepository _storeRepository;

        public StoreService(StoreRepository storeRepository)
        {
            _storeRepository = storeRepository;
        }
        public async Task<List<StoreEntity>> GetAllAsync()
        {
            var list = await _storeRepository.GetAllAsync();
            return list.OrderBy(x => x.StoreCode).ToList();
        }

        public async Task<StoreEntity?> GetStoreAsync(string storeCode) =>
            await _storeRepository.GetOneAsync(s => s.StoreCode == storeCode, null);
    }
}
