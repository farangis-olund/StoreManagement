namespace Infrastructure.Services;

// A saved list change updates open order screens without reloading their carts.
public sealed class ClearanceCatalog
{
    private IReadOnlySet<string> _articles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public event EventHandler<EventArgs>? Changed;
    public bool Contains(string article) => _articles.Contains(article);
    public void Publish(IEnumerable<string> articles)
    {
        _articles = articles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
