using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;

namespace PresentationWpf.Services;

public class UserSessionService
{
    private readonly DatabaseContext _context;
    private readonly OrganizationInfoService _organizationInfoService;
    private readonly CurrencyService _currencyService;

    public UserSessionService(
        DatabaseContext context,
        OrganizationInfoService organizationInfoService,
        CurrencyService currencyService)
    {
        _context = context;
        _organizationInfoService = organizationInfoService;
        _currencyService = currencyService;
    }

    public int UserId { get; private set; } 
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string OrganizationDisplayName { get; private set; } = string.Empty;
    public string ActiveCurrencyCode { get; private set; } = "EUR";
    public double ExchangeRate { get; private set; }
    public double CustomerDiscountPercentage { get; private set; }
    public bool IsLoggedIn { get; private set; }

    // ✅ Unified login + session initialization
    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Логин или пароль не указаны");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.UserName == username);
            if (user == null || user.Password != password)
                return false;

            // Populate user info
            UserId = user.Id;
            FirstName = user.FirstName;
            LastName = user.LastName;
            UserName = user.UserName;
            IsLoggedIn = true;

            // Load organization and rates
            await InitializeEnvironmentAsync();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UserSession] Login error: {ex.Message}");
            return false;
        }
    }

    // ✅ Load environment data (org info, exchange rate, etc.)
    private async Task InitializeEnvironmentAsync()
    {
        try
        {
            OrganizationDisplayName = (await _organizationInfoService.GetShopDisplayAsync())?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UserSession] Error loading organization name: {ex.Message}");
        }

        try
        {
            var rate = await _currencyService.GetLatestRateAsync(ActiveCurrencyCode);
            ExchangeRate = rate ?? 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UserSession] Error loading exchange rate: {ex.Message}");
            ExchangeRate = 0;
        }
    }

    public void Logout()
    {
        UserId = 0;
        FirstName = string.Empty;
        LastName = string.Empty;
        UserName = string.Empty;
        IsLoggedIn = false;
    }

    public event Action<double>? ExchangeRateChanged;

    public async Task RefreshExchangeRateAsync(string? currencyCode = null)
    {
        try
        {
            // Update active code if user selected a new one
            if (!string.IsNullOrEmpty(currencyCode))
                ActiveCurrencyCode = currencyCode.Trim().ToUpper();

            // Get the latest rate
            var rate = await _currencyService.GetLatestRateAsync(ActiveCurrencyCode);
            ExchangeRate = rate ?? 0;

            // Notify subscribers (e.g. ViewModels)
            ExchangeRateChanged?.Invoke(ExchangeRate);

            Debug.WriteLine($"[UserSession] Exchange rate updated: {ActiveCurrencyCode} = {ExchangeRate}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UserSession] Error refreshing exchange rate: {ex.Message}");
            ExchangeRate = 0;
            ExchangeRateChanged?.Invoke(ExchangeRate);
        }
    }

    // in UserSessionService

    public void UpdateCurrentUser(UserEntity user)
    {
        // если сейчас никто не залогинен – ничего не делаем
        if (!IsLoggedIn)
            return;

        // если обновляется другой пользователь – тоже выходим
        if (UserId != user.Id)
            return;

        // Обновляем данные текущей сессии
        FirstName = user.FirstName;
        LastName = user.LastName;
        UserName = user.UserName;

        // при необходимости здесь можно добавить ещё что-то,
        // например, обновление скидки, роли и т.п.
    }

}
