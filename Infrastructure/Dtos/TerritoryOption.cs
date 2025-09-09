namespace Infrastructure.Dtos;

public sealed class TerritoryOption
{
	public string? Value { get; init; }          // null => All territories
	public string Display { get; init; } = "";
	public override string ToString() => Display; // nice debugging
}