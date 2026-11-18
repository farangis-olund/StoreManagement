using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Dtos;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class ProductRequestUiTests
{
    public static int Run()
    {
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            while (folder != null && !File.Exists(Path.Combine(folder.FullName, "PresentationWpf", "App.xaml"))) folder = folder.Parent;
            Check(folder != null, "Cannot find application resources.");
            // Load the actual styles without App's startup, database copy or migration side effects.
            var source = XDocument.Load(Path.Combine(folder!.FullName, "PresentationWpf", "App.xaml")).Root!;
            XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var resources = new XElement(ns + "ResourceDictionary",
                source.Attributes().Where(x => x.IsNamespaceDeclaration), source.Element(ns + "Application.Resources")!.Elements());
            foreach (var setter in resources.Descendants(ns + "EventSetter").ToArray()) setter.Remove();
            foreach (var element in resources.Descendants().Where(x => x.Name.NamespaceName.StartsWith("clr-namespace:")))
                element.Name = XName.Get(element.Name.LocalName, element.Name.NamespaceName + ";assembly=PresentationWpf");
            foreach (var attribute in resources.Attributes().Where(x => x.IsNamespaceDeclaration && x.Value.StartsWith("clr-namespace:")))
                attribute.Value += ";assembly=PresentationWpf";
            app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());
            CheckProductRequestsMenu();
            AdminPermissionUiTests.Run();
            UserManagerUiTests.Run();
            RetailManagerUiTests.Run();
            OfficialMonthlyPlanUiTests.Run();
            StoreExchangeReportUiTests.Run();
            WarehousePlaceImportUiTests.Run();
            CheckRetailSearch();
            CheckRetail();
            CheckDeliverySelection();
            RetailCustomerSelectionUiTests.Run();
            ReturnSearchUiTests.Run();
            ReturnEntryUiTests.Run();
            RetailLimitUiTests.Run();
            RetailMonthlyPlanLimitUiTests.Run();
            RetailBarterUiTests.Run();
            BarterHistoryUiTests.Run();
            BarterInvoicePdfTests.Run();
            PendingOrdersUiTests.Run();
            OrderOperationsUiTests.Run();
            CheckRequestNoticeReset();
            CheckWelcomeNavigation();
            CheckDraftForm();
            CheckJournal();
            CheckPrinting();
            DeliveryReportPdfTests.Run();
            ReconciliationReportPdfTests.Run();
            PdfPrinterTests.Run();
            ProductRequestExcelTests.Run();
            Console.WriteLine("PASS: permission-controlled sidebar menu, exact article and keyword search, Retail request controls, stock rules, home link navigation, draft form, journal details, bulk-action availability and multi-page printing");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void CheckProductRequestsMenu()
    {
        object State(bool allowed) => new
        {
            IsLoggedIn = true, CanViewOrders = false, CanViewReturns = false, CanViewCustomers = false,
            CanViewInventory = true, CanViewProductRequests = allowed, CanViewExpenses = false,
            CanViewReports = false, CanViewStatistics = false, CanViewAdmin = false, CanViewSettings = false,
            CurrentViewModel = (object?)null
        };
        var window = new PresentationWpf.MainWindow(null!) { DataContext = State(false) };
        var root = (FrameworkElement)window.Content;
        Layout(root);
        var item = (RadioButton)window.FindName("ProductRequestsMenuItem");
        Check(item.Visibility == Visibility.Collapsed, "Journal menu is visible without its permission.");
        window.DataContext = State(true);
        Layout(root);
        Check(item.Visibility == Visibility.Visible, "Journal menu is hidden when its permission is granted without Admin.");
        var parent = (StackPanel)LogicalTreeHelper.GetParent(item);
        var previous = parent.Children[parent.Children.IndexOf(item) - 1] as RadioButton;
        Check(previous?.GetBindingExpression(System.Windows.Controls.Primitives.ButtonBase.CommandProperty)?.ParentBinding.Path.Path
            == "NavigateToStockCommand", "Journal menu must immediately follow Inventory.");
        Check(item.GetBindingExpression(System.Windows.Controls.Primitives.ButtonBase.CommandProperty)?.ParentBinding.Path.Path
            == "NavigateToProductRequestsCommand", "Journal menu is not connected to its navigation command.");
        window.DataContext = State(false);
        Layout(root);
        Check(item.Visibility == Visibility.Collapsed, "Revoking the journal permission must hide its menu item.");
        var admin = new AdminView();
        Layout(admin);
        Check(!Descendants<RadioButton>(admin).Any(x => Equals(x.Content, "Заявки на товары")),
            "Journal menu must be removed from Administration.");
        window.Close();
    }

    private static void CheckRetailSearch()
    {
        var vm = new RetailViewModel(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new PermissionService(), null!);
        Check(vm.SearchAllWords, "All-keyword search must be enabled by default.");
        var exact = new ProductModel { ArticleNumber = "AB 123 CD", ProductName = "Точный товар", Quentity = 0 };
        var other = new ProductModel { ArticleNumber = "AB 999", ProductName = "123 CD", Quentity = 5 };
        var oil = new ProductModel { ArticleNumber = "FILTER-A", ProductName = "Фильтр", BrandName = "Масляный" };
        var air = new ProductModel { ArticleNumber = "FILTER-B", ProductName = "Фильтр воздушный" };
        foreach (var product in new[] { exact, other, oil, air }) vm.ProductList.Add(product);
        var listView = CollectionViewSource.GetDefaultView(vm.ProductList);
        listView.Filter = (Predicate<object>)typeof(RetailViewModel).GetMethod("FilterProducts", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Predicate<object>), vm);
        typeof(RetailViewModel).GetProperty(nameof(vm.ProductListView))!.SetValue(vm, listView);

        vm.SelectedProduct = other;
        foreach (var allWords in new[] { true, false })
        {
            vm.SearchAllWords = allWords;
            vm.FilterText = "  ab 123 cd  ";
            Check(listView.Cast<ProductModel>().Single() == exact && vm.RequestArticle == exact.ArticleNumber,
                "An exact spaced article must exclude products matching its individual words, in either search mode.");
        }
        vm.SearchAllWords = true;
        vm.FilterText = "AB 123";
        Check(listView.Cast<ProductModel>().Count() == 2, "An incomplete article must still use keyword search.");
        vm.FilterText = "масляный фильтр";
        Check(listView.Cast<ProductModel>().Single() == oil, "All keywords must match, including words in different product fields.");
        vm.SearchAllWords = false;
        Check(listView.Cast<ProductModel>().Count() == 2, "Unchecking all-keyword search must restore matching any keyword.");
        vm.FilterText = "UNKNOWN ARTICLE";
        Check(listView.IsEmpty && vm.RequestArticle == "UNKNOWN ARTICLE" && vm.IsRequestEntryVisible,
            "A missing spaced article must remain available for creating a request.");
        vm.FilterText = "   ";
        Check(listView.Cast<ProductModel>().Count() == 4, "Clearing the search must remove the exact-article restriction.");
        vm.ResetState();
        Check(vm.SearchAllWords, "Reopening Retail must enable all-keyword search.");
    }

    private static void CheckRetail()
    {
        var vm = new RetailViewModel(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new PermissionService(), null!);
        var zero = new ProductModel { ArticleNumber = "ZERO", ProductName = "Нет в наличии", BrandName = "Brand", Quentity = 0 };
        var stock = new ProductModel { ArticleNumber = "SHORT", ProductName = "Товар", BrandName = "Brand", Quentity = 2 };
        vm.ProductList.Add(zero); vm.ProductList.Add(stock);
        var listView = CollectionViewSource.GetDefaultView(vm.ProductList);
        listView.Filter = (Predicate<object>)typeof(RetailViewModel).GetMethod("FilterProducts", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Predicate<object>), vm);
        typeof(RetailViewModel).GetProperty(nameof(vm.ProductListView))!.SetValue(vm, listView);
        var view = new RetailView { DataContext = vm };
        vm.FilterText = "ZERO";
        Layout(view);
        Check(Descendants<CheckBox>(view).Any(x => Equals(x.Content, "Поиск всех кл.слов") && x.IsChecked == true),
            "The all-keyword checkbox must be checked when Retail opens.");
        Check(listView.Cast<ProductModel>().Single() == zero && vm.IsRequestEntryVisible && vm.RequestArticle == "ZERO",
            "Zero stock must remain searchable and offer a request.");
        Check(!vm.AddToChartCommand.CanExecute(zero), "Zero stock can be added to the cart.");
        vm.FilterText = "NEW-ARTICLE";
        Check(vm.IsRequestEntryVisible && vm.RequestArticle == "NEW-ARTICLE", "Missing article request is not offered.");
        vm.SelectedProduct = zero;
        Check(vm.RequestArticle == "NEW-ARTICLE", "A stale selection overrides the missing article being searched.");
        vm.SendProductRequestCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(vm.RequestStatus.Contains("клиента"), "Requests without a customer were not blocked.");
        var cart = (DataGrid)view.FindName("CartGrid");
        var quantity = cart.Columns.OfType<DataGridTextColumn>().Single(x => (x.Binding as Binding)?.Path.Path == "OrderQuentity");
        Check(((Binding)quantity.Binding).UpdateSourceTrigger == UpdateSourceTrigger.LostFocus,
            "Quantity must be committed once, rather than generating demand for intermediate keystrokes.");
        typeof(RetailViewModel).GetField("_selectedCustomer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(vm, new Customer { Id = "C1", FullName = "Клиент" });
        typeof(RetailViewModel).GetField("_customerBalance", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, 0m);
        vm.AvailableRestriction = 10000m;
        Check(vm.AddToChartCommand.CanExecute(stock) && !vm.AddToChartCommand.CanExecute(zero), "Stock-sensitive add rule is incorrect.");
        vm.FilterText = "ZERO";
        vm.RequestQuantity = "-4";
        vm.SendProductRequestCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(vm.RequestStatus.Contains("больше нуля"), "Invalid request quantity was accepted.");
        Layout(view);
        Render(view, "retail-requests.png");
        Layout(view, 1700);
        Render(view, "retail-requests-wide.png");
    }

    private static void CheckDeliverySelection()
    {
        // Keep customer loading pending so these checks never connect to a working database.
        var factory = new DelayedRequestFactory();
        var permissions = new PermissionService();
        permissions.SetPermissions([Infrastructure.Constants.PermissionCatalog.OrdersBarter]);
        var customers = new CustomerService(null!, factory, null!, null!, null!, null!, null!);
        var vm = new RetailViewModel(null!, null!, null!, customers, null!, null!, null!, null!, null!, null!,
            new CustomerFinanceService(factory), permissions, null!);
        var first = new Customer { Id = "C1", FullName = "Первый клиент" };
        var second = new Customer { Id = "C2", FullName = "Второй клиент" };
        vm.AllCustomers.Add(first);
        vm.AllCustomers.Add(second);
        var courier = new Courier { Id = "D1", FullName = "Доставщик", Active = true };
        var storekeeper = new Storekeeper { Id = "S1", FullName = "Сборщик", Active = true };
        vm.CourierList.Add(courier);
        vm.StorekeeperList.Add(storekeeper);
        var view = new RetailView { DataContext = vm };
        Layout(view);
        ComboBox Selection(string property) => Descendants<ComboBox>(view).First(x =>
            x.GetBindingExpression(ComboBox.SelectedItemProperty)?.ParentBinding.Path.Path == property);
        var courierInput = Selection(nameof(vm.SelectedCourier));
        var storekeeperInput = Selection(nameof(vm.SelectedStorekeeper));
        var customerInput = Selection(nameof(vm.SelectedCustomer));
        var save = Descendants<Button>(view).Single(x =>
            x.GetBindingExpression(Button.CommandProperty)?.ParentBinding.Path.Path == nameof(vm.SaveOrderCommand));
        void FinanceReady()
        {
            typeof(RetailViewModel).GetField("_customerBalance", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, 0m);
            vm.AvailableRestriction = 10000m;
            vm.SaveOrderCommand.NotifyCanExecuteChanged();
            Layout(view);
        }
        void SelectDelivery()
        {
            courierInput.SelectedItem = courier;
            storekeeperInput.SelectedItem = storekeeper;
            Layout(view);
        }
        void CheckCleared(string action)
        {
            Layout(view);
            Check(vm.SelectedCourier == null && vm.SelectedStorekeeper == null &&
                courierInput.SelectedItem == null && storekeeperInput.SelectedItem == null &&
                courierInput.Text == "" && storekeeperInput.Text == "",
                action + " must clear delivery objects and their displayed values.");
            Check(!vm.SaveOrderCommand.CanExecute(null) && !save.IsEnabled,
                action + " must block an ordinary order until delivery is selected again.");
        }

        customerInput.SelectedItem = first;
        FinanceReady();
        Check(!vm.SaveOrderCommand.CanExecute(null), "An ordinary order needs both delivery selections.");
        courierInput.SelectedItem = courier;
        Check(!vm.SaveOrderCommand.CanExecute(null), "Selecting only a courier must not enable saving.");
        storekeeperInput.SelectedItem = storekeeper;
        Layout(view);
        Check(vm.SaveOrderCommand.CanExecute(null) && save.IsEnabled, "Valid delivery selections must enable saving.");

        customerInput.SelectedItem = second;
        Check(vm.SelectedCustomer == second, "The customer input must update the actual selected customer.");
        FinanceReady();
        CheckCleared("Changing the customer");
        SelectDelivery();
        courierInput.Text = "";
        Layout(view);
        Check(vm.SelectedCourier == null && !vm.SaveOrderCommand.CanExecute(null) && !save.IsEnabled,
            "Clearing the courier input must clear its object and disable saving.");
        SelectDelivery();
        storekeeperInput.Text = "";
        Layout(view);
        Check(vm.SelectedStorekeeper == null && !vm.SaveOrderCommand.CanExecute(null) && !save.IsEnabled,
            "Clearing the storekeeper input must clear its object and disable saving.");

        SelectDelivery();
        vm.SelectedCustomer = null;
        CheckCleared("Clearing the customer");
        // ResetState must also clear delivery if the customer was already null.
        SelectDelivery();
        vm.ResetState();
        CheckCleared("Reopening Orders");
        customerInput.SelectedItem = first;
        FinanceReady();
        CheckCleared("Selecting a customer after reopening Orders");
        SelectDelivery();
        vm.SelectedCourier = new Courier { Id = courier.Id, FullName = courier.FullName };
        Check(!vm.SaveOrderCommand.CanExecute(null), "A stale courier object outside the current list must not allow saving.");
        vm.SelectedCourier = courier;
        vm.SelectedStorekeeper = new Storekeeper { Id = storekeeper.Id, FullName = storekeeper.FullName };
        Check(!vm.SaveOrderCommand.CanExecute(null), "A stale storekeeper object outside the current list must not allow saving.");

        vm.SelectedCourier = null;
        vm.SelectedStorekeeper = null;
        typeof(RetailViewModel).GetField("_isBarter", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, true);
        Check(vm.SaveOrderCommand.CanExecute(null), "Authorized barter must retain its existing delivery exemption.");
        Console.WriteLine("PASS: customer changes and reopening Orders clear delivery; empty or stale delivery cannot enable an ordinary order; barter remains supported");
    }

    private static void CheckRequestNoticeReset()
    {
        var factory = new DelayedRequestFactory();
        var permissions = new PermissionService();
        var service = new ProductRequestService(factory, permissions);
        var vm = new RetailViewModel(null!, new UserSessionService(null!, null!, null!), null!, null!, null!,
            null!, null!, null!, null!, null!, null!, permissions, service);
        var zero = new ProductModel { ArticleNumber = "ZERO", ProductName = "Нет в наличии", BrandName = "Brand", Quentity = 0 };
        var stock = new ProductModel { ArticleNumber = "SHORT", ProductName = "Товар", BrandName = "Brand", Quentity = 2 };
        vm.ProductList.Add(zero); vm.ProductList.Add(stock);
        var productsView = CollectionViewSource.GetDefaultView(vm.ProductList);
        productsView.Filter = (Predicate<object>)typeof(RetailViewModel).GetMethod("FilterProducts", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Predicate<object>), vm);
        typeof(RetailViewModel).GetProperty(nameof(vm.ProductListView))!.SetValue(vm, productsView);
        var view = new RetailView { DataContext = vm };
        var search = (TextBox)view.FindName("SearchBox");
        Layout(view);
        search.Text = "ZERO";
        vm.SelectedProduct = zero;
        vm.RequestQuantity = "10";
        vm.RequestStatus = "Заявка на ZERO: 10 шт. отправлена администратору.";
        vm.FilterText = "ZERO";
        Check(vm.RequestStatus.Length > 0 && vm.RequestQuantity == "10",
            "Reassigning the same search must preserve the current request state.");
        search.Text = "Brand";
        Check(vm.FilterText == "Brand" && vm.RequestStatus == "" && vm.RequestMessage == "" &&
            vm.RequestArticle == "" && vm.RequestQuantity == "1" && !vm.IsRequestEntryVisible && vm.SelectedProduct == null,
            "Typing a broad query must clear the old notice, selection and request form.");
        search.Text = "SHORT";
        Check(vm.RequestArticle == "SHORT" && vm.RequestMessage == "" && !vm.IsRequestEntryVisible,
            "An in-stock article must not retain the previous out-of-stock prompt.");
        search.Text = "ZERO";
        Check(vm.RequestArticle == "ZERO" && vm.IsRequestEntryVisible && vm.RequestStatus == "",
            "A new out-of-stock search must show a fresh request form.");
        typeof(RetailViewModel).GetField("_selectedCustomer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(vm, new Customer { Id = "C1", FullName = "Клиент" });
        vm.RequestQuantity = "4";
        var send = vm.SendProductRequestCommand.ExecuteAsync(null);
        Check(factory.WasRequested && !send.IsCompleted, "The request must be awaiting its database operation.");
        search.Text = "NEW-ARTICLE";
        vm.RequestQuantity = "12";
        factory.Completion.SetException(new InvalidOperationException("Failure from the previous article"));
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Check(send.IsCompleted, "The old request must finish after the delayed failure.");
        send.GetAwaiter().GetResult();
        Check(vm.RequestStatus == "" && vm.RequestArticle == "NEW-ARTICLE" && vm.RequestQuantity == "12" &&
            vm.IsRequestEntryVisible && vm.RequestSubmitLabel == "Сохранить заявку",
            "A late result for the old article must not overwrite the new article's request form.");
        vm.ProductList.Remove(stock);
        search.Text = "";
        Check(vm.RequestStatus == "" && vm.RequestMessage == "" && vm.RequestArticle == "" && !vm.IsRequestEntryVisible,
            "Clearing search must clear the prompt even when the catalogue contains just one product.");
        vm.ProductList.Add(stock);
        search.Text = "Brand";
        vm.SelectedProduct = zero;
        vm.RequestStatus = "Сообщение для ZERO";
        vm.RequestQuantity = "8";
        vm.SelectedProduct = stock;
        Check(vm.RequestStatus == "" && vm.RequestArticle == "SHORT" && vm.RequestQuantity == "1" && !vm.IsRequestEntryVisible,
            "Selecting a different article must also clear the previous article's notice.");
        Console.WriteLine("PASS: changing or clearing search resets old notices and request fields; delayed old errors cannot overwrite a new article");
    }

    private sealed class DelayedRequestFactory : IDbContextFactory<DatabaseContext>
    {
        public TaskCompletionSource<DatabaseContext> Completion { get; } = new();
        public bool WasRequested { get; private set; }
        public DatabaseContext CreateDbContext() => throw new InvalidOperationException("No database is used by this test.");
        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            WasRequested = true;
            return Completion.Task;
        }
    }

    private static void CheckWelcomeNavigation()
    {
        var session = new UserSessionService(null!, null!, null!);
        var home = new Welcome { DataContext = new WelcomeViewModel(session) };
        var draftsVm = new MyNewArticlesViewModel(session, null!);
        var drafts = new MyNewArticlesView { DataContext = draftsVm };
        var window = new Window { Content = home };
        draftsVm.GoHomeCommand = new RelayCommand(() => window.Content = home);
        window.DataContext = new
        {
            NavigateToMyNewArticlesCommand = new RelayCommand(() => window.Content = drafts)
        };
        Layout(home);
        Check(!Descendants<DataGrid>(home).Any() && !Descendants<TextBox>(home).Any(),
            "Home must show a link, without the draft list or editor.");
        var link = (Hyperlink)home.FindName("MyNewArticlesLink");
        Check(new TextRange(link.ContentStart, link.ContentEnd).Text == "Мои новые артикулы" && link.Command != null,
            "Home must expose the requested link bound to navigation.");
        Render(home, "welcome-home.png");
        link.Command!.Execute(null);
        Check(ReferenceEquals(window.Content, drafts), "The home link must open the separate draft view.");
        Layout(drafts);
        var back = Descendants<Button>(drafts).Single(x => Equals(x.Content, "← На главную"));
        Check(back.Command != null, "The draft view must offer navigation back home.");
        back.Command!.Execute(null);
        Layout(home);
        Check(ReferenceEquals(window.Content, home) && !Descendants<DataGrid>(home).Any(),
            "Returning home must restore the link without an open draft form.");
        window.Close();
    }

    private static void CheckDraftForm()
    {
        var vm = new MyNewArticlesViewModel(new UserSessionService(null!, null!, null!), null!);
        var draft = new ProductRequestEntity
        {
            ArticleNumber = "NEW-ARTICLE", Quantity = 7, CustomerName = "Клиент", CreatedAt = DateTime.Today,
            ProductName = "Новый товар", BrandName = "Бренд", Marka = "Марка", Model = "Модель", GroupName = "Группа"
        };
        vm.Drafts.Add(draft); vm.SelectedDraft = draft;
        var view = new MyNewArticlesView { DataContext = vm };
        Layout(view);
        var inputs = Descendants<TextBox>(view).ToList();
        var brand = inputs.Single(x => x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "SelectedDraft.BrandName");
        brand.Text = "Другой бренд";
        brand.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Check(draft.BrandName == "Другой бренд", "Draft editor does not save edits into the selected request.");
        Check(inputs.All(x => !(x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path ?? "").Contains("Price")),
            "Draft editor exposes a price field.");
        Render(view, "new-article-drafts.png");
    }

    private static void CheckJournal()
    {
        var vm = new ProductRequestsViewModel(null!) { SelectedTab = 2 };
        var records = new[]
        {
            new ProductRequestEntity { ArticleNumber = "NEW-ARTICLE", Quantity = 7, CustomerId = "C1", CustomerName = "Первый клиент", UserName = "Пользователь 1", BrandName = "Бренд", ProductName = "Новый товар", CreatedAt = DateTime.Today },
            new ProductRequestEntity { ArticleNumber = "NEW-ARTICLE", Quantity = 3, CustomerId = "C2", CustomerName = "Второй клиент", UserName = "Пользователь 2", BrandName = "Бренд", ProductName = "Новый товар", CreatedAt = DateTime.Today }
        };
        var row = ProductRequestSummary.Group(records).Single();
        vm.Rows.Add(row);
        vm.ShowDetailsCommand.Execute(row);
        var view = new ProductRequestsView { DataContext = vm };
        Layout(view);
        var export = (Button)view.FindName("ExportExcelButton");
        var toolbar = (Panel)LogicalTreeHelper.GetParent(export);
        Check(toolbar.Children[toolbar.Children.IndexOf(export) - 1] is Button { Content: "Печать" },
            "Excel export must immediately follow Print.");
        Check(export.IsEnabled, "Displayed new articles must be exportable.");
        Check(vm.Rows.Single().Quantity == 10 && vm.Details.Count == 2 && vm.IsNewArticles && !vm.CanDelete,
            "Journal summary and details disagree.");
        Render(view, "product-request-journal.png");
        Check(Descendants<CheckBox>(view).Any(x => Equals(x.Content, "Обработано") && x.Visibility == Visibility.Visible),
            "New article processing checkbox is missing: " + string.Join(", ", Descendants<CheckBox>(view).Select(x => $"{x.Content}: {x.Visibility}")));
        var deleteDisplayed = (Button)view.FindName("DeleteDisplayedButton");
        Check(deleteDisplayed.Visibility == Visibility.Collapsed && !vm.DeleteDisplayedCommand.CanExecute(null),
            "Bulk deletion must not be available for new articles.");
        var markDisplayed = (Button)view.FindName("MarkDisplayedProcessedButton");
        Check(markDisplayed.Visibility == Visibility.Visible && markDisplayed.IsEnabled &&
            vm.MarkDisplayedProcessedCommand.CanExecute(null), "New articles must offer bulk processing.");
        vm.IsBusy = true;
        Check(!vm.CanExportExcel, "Export must be blocked while loading requests.");
        Check(!vm.MarkDisplayedProcessedCommand.CanExecute(null), "Bulk processing must be disabled while busy.");
        vm.IsBusy = false;
        // Even direct execution must be guarded for the protected tab.
        vm.DeleteDisplayedCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        foreach (var tab in new[] { 0, 1 })
        {
            vm.SelectedTab = tab;
            Layout(view);
            Check(!export.IsEnabled, "Export must be disabled for an empty tab.");
            Check(deleteDisplayed.Visibility == Visibility.Visible && !deleteDisplayed.IsEnabled,
                "Bulk deletion must be visible but disabled for an empty stock-request tab.");
            vm.Rows.Add(row);
            vm.ShowDetailsCommand.Execute(row);
            Layout(view);
            Check(export.IsEnabled, "Both stock-request tabs must allow exporting displayed rows.");
            Check(markDisplayed.Visibility == Visibility.Collapsed && !vm.MarkDisplayedProcessedCommand.CanExecute(null),
                "Bulk processing must be unavailable on both stock-request tabs.");
            vm.MarkDisplayedProcessedCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Check(deleteDisplayed.IsEnabled && vm.DeleteDisplayedCommand.CanExecute(null),
                "Adding displayed requests must enable bulk deletion.");
            vm.IsBusy = true;
            Check(!vm.DeleteDisplayedCommand.CanExecute(null), "Bulk deletion must be disabled during an operation.");
            vm.IsBusy = false;
            Layout(view, 1700);
            var grid = (DataGrid)view.FindName("RequestsGrid");
            Check(grid.Columns.Single(x => Equals(x.Header, "Наименование")).ActualWidth <= 320,
                "The product name column must stay compact on a wide window.");
            Render(view, $"product-request-journal-{tab}.png");
            vm.Rows.Clear();
            Check(!vm.DeleteDisplayedCommand.CanExecute(null), "Clearing the displayed list must disable bulk deletion.");
        }
        vm.SelectedTab = 2;
        Check(!vm.MarkDisplayedProcessedCommand.CanExecute(null), "An empty new-article list must disable bulk processing.");
        vm.MarkDisplayedProcessedCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        records[0].IsProcessed = true;
        records[1].IsProcessed = true;
        vm.Rows.Add(ProductRequestSummary.Group(records).Single());
        Check(!vm.MarkDisplayedProcessedCommand.CanExecute(null), "Already processed requests must not enable bulk processing.");
        vm.MarkDisplayedProcessedCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        records[1].IsProcessed = false;
        Check(vm.MarkDisplayedProcessedCommand.CanExecute(null), "A partly processed article still needs bulk processing.");
    }

    private static void CheckPrinting()
    {
        var rows = Enumerable.Range(1, 200).Select(i => new ProductRequestSummary
        {
            ArticleNumber = $"ITEM-{i}", ProductName = "Товар для проверки печати всех страниц", BrandName = "Бренд", Quantity = i
        }).ToArray();
        var document = ProductRequestPrintDocument.Create("Недостаточный остаток", "Все бренды", rows, new Size(793, 1122));
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.ComputePageCount();
        Check(paginator.PageCount > 1, "Large request journal was clipped to one printed page.");
        Check(document.Blocks.OfType<Table>().Single().RowGroups[0].Rows.Count == 201, "Print document omitted request rows.");
    }

    private static void Layout(FrameworkElement view, double width = 1280)
    {
        view.Width = width; view.Height = 850;
        view.Measure(new Size(width, 850));
        view.Arrange(new Rect(0, 0, width, 850));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void Render(FrameworkElement view, string name)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name));
        encoder.Save(stream);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

}
