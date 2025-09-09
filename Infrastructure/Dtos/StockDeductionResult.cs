
namespace Infrastructure.Dtos;

public sealed class StockDeductionResult
{
	public List<string> NotEnoughArticles { get; } = new();

	// Success is true when nothing failed
	public bool Success => NotEnoughArticles.Count == 0;
}


public sealed record StockDeductionItem(string ArticleNumber, int Quantity);