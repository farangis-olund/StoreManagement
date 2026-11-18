namespace Infrastructure.Dtos;

public sealed record StoreExchangeReportRow(int Number, string ArticleNumber, long WarehouseQuantity,
    long IncomingQuantity, long OutgoingQuantity)
{
    public long RemainingQuantity => WarehouseQuantity - IncomingQuantity + OutgoingQuantity;
}
