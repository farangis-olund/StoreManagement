
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Infrastructure.Entities;
using Infrastructure.Services;
using PresentationWpf.Services;

namespace PresentationWpf.ViewModels;

public partial class MyNewArticlesViewModel : ObservableObject
{
    private readonly UserSessionService _session;
    private readonly ProductRequestService _requests;

    public MyNewArticlesViewModel(UserSessionService session, ProductRequestService requests)
    {
        _session = session;
        _requests = requests;
    }

    public ObservableCollection<ProductRequestEntity> Drafts { get; } = [];
    [ObservableProperty] private IRelayCommand? _goHomeCommand;
    [ObservableProperty] private ProductRequestEntity? _selectedDraft;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _message = "";

    public bool CanEditDraft => SelectedDraft != null && !IsBusy;
    partial void OnSelectedDraftChanged(ProductRequestEntity? value) => OnPropertyChanged(nameof(CanEditDraft));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanEditDraft));

    [RelayCommand]
    private async Task RefreshDrafts()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var selectedId = SelectedDraft?.Id;
            var drafts = await _requests.GetDraftsAsync(_session.UserId);
            Drafts.Clear();
            foreach (var draft in drafts) Drafts.Add(draft);
            SelectedDraft = Drafts.FirstOrDefault(x => x.Id == selectedId) ?? Drafts.FirstOrDefault();
            Message = Drafts.Count == 0 ? "Нет новых артикулов для заполнения." : $"Ожидают заполнения: {Drafts.Count}.";
        }
        catch (Exception ex) { Message = ex.GetBaseException().Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveDraft() => await SaveSelectedDraft(submit: false);

    [RelayCommand]
    private async Task SubmitDraft() => await SaveSelectedDraft(submit: true);

    private async Task SaveSelectedDraft(bool submit)
    {
        if (!CanEditDraft || SelectedDraft == null) return;
        var draft = SelectedDraft;
        IsBusy = true;
        try
        {
            await _requests.SaveDraftAsync(draft, _session.UserId, submit);
            Message = submit ? $"Заявка на {draft.ArticleNumber} отправлена администратору." : "Черновик сохранён.";
            if (submit)
            {
                Drafts.Remove(draft);
                SelectedDraft = Drafts.FirstOrDefault();
            }
        }
        catch (Exception ex) { Message = ex.GetBaseException().Message; }
        finally { IsBusy = false; }
    }
}
