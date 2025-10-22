

namespace Infrastructure.Dtos;

public class ExchangeProductDto
    {
        public string ArticleNumber { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string BrandName { get; set; } = "";
        public string Marka { get; set; } = "";
        public string Model { get; set; } = "";
        public string StoreCode { get; set; } = "";
        public string WarehousePlace { get; set; } = "";
        public int Quantity { get; set; }
        public int Debt { get; set; }
    }

