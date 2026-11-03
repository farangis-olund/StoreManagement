using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using PresentationWpf.Dtos;

namespace PresentationWpf.ViewModels;

public partial class RetailViewModel
{
    private readonly ProductRequestService _productRequestService;
    private readonly HashSet<ProductModel> _subscribedCartProducts = [];
    private readonly SemaphoreSlim _automaticRequestLock = new(1, 1);
    private readonly Dictionary<(int User, string Customer, string Article), Guid> _cartRequestIds = [];
    private readonly Dictionary<Guid, AutomaticRequest> _pendingAutomaticRequests = [];
    private Guid _manualRequestId = Guid.NewGuid();
    private sealed record AutomaticRequest(Guid Id, int UserId, string CustomerId, string Article,
        int Requested, int Available, int Missing);

    [ObservableProperty] private bool _isRequestEntryVisible;
    [ObservableProperty] private string _requestArticle = "";
    [ObservableProperty] private string _requestQuantity = "1";
    [ObservableProperty] private string _requestSubmitLabel = "Отправить заявку";
    [ObservableProperty] private string _requestMessage = "";
    [ObservableProperty] private string _requestStatus = "";
    [ObservableProperty] private bool _hasPendingProductRequests;

    partial void OnHasPendingProductRequestsChanged(bool value) => SaveOrderCommand.NotifyCanExecuteChanged();

    private (string Article, ProductModel? Product)? ResolveRequestTarget()
    {
        var text = FilterText?.Trim() ?? "";
        if (SelectedProduct != null && ProductList.Contains(SelectedProduct) && FilterProducts(SelectedProduct))
            return (SelectedProduct.ArticleNumber, SelectedProduct);
        if (text.Length == 0) return null;
        var exact = ProductList.FirstOrDefault(p => string.Equals(p.ArticleNumber, text, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return (exact.ArticleNumber, exact);
        var matches = ProductList.Where(FilterProducts).Take(2).ToList();
        if (matches.Count == 1) return (matches[0].ArticleNumber, matches[0]);
        if (text.Length > 0 && matches.Count == 0) return (text, null);
        return null;
    }

    private void ResetProductRequestEntry()
    {
        _manualRequestId = Guid.NewGuid();
        IsRequestEntryVisible = false;
        RequestArticle = "";
        RequestQuantity = "1";
        RequestSubmitLabel = "Отправить заявку";
        RequestMessage = "";
        RequestStatus = "";
    }

    private void UpdateProductRequestPrompt()
    {
        var target = ResolveRequestTarget();
        if (target == null)
        {
            ResetProductRequestEntry();
            return;
        }
        if (!string.Equals(RequestArticle, target.Value.Article, StringComparison.OrdinalIgnoreCase))
        {
            ResetProductRequestEntry();
            RequestArticle = target.Value.Article;
        }
        if (target.Value.Product == null)
        {
            RequestSubmitLabel = "Сохранить заявку";
            RequestMessage = $"Артикул «{RequestArticle}» не найден. Укажите количество и сохраните заявку. " +
                "Характеристики товара можно заполнить в главном меню.";
            IsRequestEntryVisible = true;
        }
        else if (target.Value.Product.Quentity <= 0)
        {
            RequestSubmitLabel = "Отправить заявку";
            RequestMessage = "Товар сейчас отсутствует на складе. Создайте заявку, указав необходимое количество.";
            IsRequestEntryVisible = true;
        }
        else
        {
            RequestSubmitLabel = "Отправить заявку";
            RequestMessage = IsRequestEntryVisible ? "Укажите недостающее количество товара." : "";
        }
    }

    [RelayCommand]
    private void OpenProductRequest()
    {
        UpdateProductRequestPrompt();
        if (string.IsNullOrWhiteSpace(RequestArticle))
        {
            MessageBox.Show("Выберите товар в результатах поиска или введите новый артикул.", "Заявка на товар");
            return;
        }
        IsRequestEntryVisible = true;
        if (string.IsNullOrEmpty(RequestMessage)) RequestMessage = "Укажите недостающее количество товара.";
    }

    [RelayCommand]
    private async Task SendProductRequest()
    {
        if (SelectedCustomer == null)
        {
            RequestStatus = "Выберите клиента перед созданием заявки.";
            return;
        }
        if (!int.TryParse(RequestQuantity?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            RequestStatus = "Укажите целое количество больше нуля.";
            return;
        }
        var article = RequestArticle;
        var id = _manualRequestId;
        var user = _userSessionService.UserId;
        var customer = SelectedCustomer.Id;
        try
        {
            var saved = await _productRequestService.CreateAsync(id, article, quantity, user, customer);
            // Saving still completes, but its result must not replace a newer search's request form.
            if (id != _manualRequestId) return;
            RequestStatus = saved.Kind == ProductRequestKind.NewArticle
                ? $"Заявка на {saved.ArticleNumber} сохранена. Откройте главное меню, заполните характеристики и отправьте администратору."
                : $"Заявка на {saved.ArticleNumber}: {saved.Quantity} шт. отправлена администратору.";
            // Keep the ID until the next deliberate request to make a failed/retried send idempotent.
            _manualRequestId = Guid.NewGuid();
            IsRequestEntryVisible = false;
            RequestQuantity = "1";
        }
        catch (Exception ex)
        {
            if (id == _manualRequestId)
                RequestStatus = $"Заявка не отправлена: {ex.GetBaseException().Message}";
        }
    }

    private async void OnOrderQuantityExceeded(ProductModel product, int requested)
    {
        if (_isAdjustingProductLimit || SelectedCustomer == null || !SelectedProductList.Contains(product)) return;
        var key = (_userSessionService.UserId, SelectedCustomer.Id, product.ArticleNumber);
        if (!_cartRequestIds.TryGetValue(key, out var id)) _cartRequestIds[key] = id = Guid.NewGuid();
        var available = Math.Max(0, product.Quentity);
        _pendingAutomaticRequests[id] = new AutomaticRequest(id, key.Item1, key.Item2, key.Item3,
            requested, available, requested - available);
        HasPendingProductRequests = true;
        await SavePendingProductRequests();
    }

    [RelayCommand]
    private async Task RetryProductRequests() => await SavePendingProductRequests();

    private async Task SavePendingProductRequests()
    {
        await _automaticRequestLock.WaitAsync();
        try
        {
            foreach (var request in _pendingAutomaticRequests.Values.ToArray())
            {
                try
                {
                    await _productRequestService.CreateAsync(request.Id, request.Article, request.Missing,
                        request.UserId, request.CustomerId, automaticShortage: true);
                    if (_pendingAutomaticRequests.TryGetValue(request.Id, out var latest) && latest == request)
                        _pendingAutomaticRequests.Remove(request.Id);
                    RequestStatus = "";
                    var message = $"Вы указали {request.Requested} шт., но на складе только {request.Available} шт. " +
                        $"На недостающие {request.Missing} шт. создана заявка и отправлена администратору.";
                    MessageBox.Show(message, "Недостаточный остаток", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    RequestStatus = $"Не удалось сохранить заявку на {request.Article}: {ex.GetBaseException().Message}. " +
                        "Нажмите «Повторить отправку заявок».";
                    MessageBox.Show(RequestStatus, "Заявка не сохранена", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                }
            }
        }
        finally
        {
            HasPendingProductRequests = _pendingAutomaticRequests.Count > 0;
            _automaticRequestLock.Release();
        }
    }
}
