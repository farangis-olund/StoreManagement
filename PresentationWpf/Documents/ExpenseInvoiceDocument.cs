using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PresentationWpf.ViewModels;

namespace PresentationWpf.Documents
{
    public class ExpenseInvoiceDocument : IDocument
    {
        private readonly ExpenseInvoiceViewModel _vm;

        public ExpenseInvoiceDocument(ExpenseInvoiceViewModel vm)
        {
            _vm = vm;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.7f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Магазин: ").SemiBold();
                            text.Span(_vm.ShopName);
                        });

                        row.ConstantItem(200).AlignRight().Text($"Дата: {_vm.Date:dd.MM.yyyy}");
                    });

                    header.Item().PaddingTop(4).LineHorizontal(0.5f);
                });

                page.Content().Column(content =>
                {
                    content.Spacing(8);

                    content.Item().AlignCenter().Text(text =>
                    {
                        text.Span("ЧЕК РАСХОДА").Bold().FontSize(14);
                    });

                    content.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.4f);
                            cols.RelativeColumn(2.6f);
                        });

                        void Row(string label, string? value)
                        {
                            table.Cell()
                                .Border(0.25f)
                                .BorderColor(Colors.Grey.Medium)
                                .Background(Colors.Grey.Lighten5)
                                .Padding(4)
                                .Text(label)
                                .SemiBold();

                            table.Cell()
                                .Border(0.25f)
                                .BorderColor(Colors.Grey.Medium)
                                .Padding(4)
                                .Text(value ?? "");
                        }

                        Row("№ документа", _vm.InvoiceNumber);
                        //Row("Код", _vm.PersonCode);
                        Row("Тип", _vm.PersonType);
                        Row("ФИО", _vm.PersonName);
                        Row("Причина", _vm.Reason);
                        Row("Примечание", _vm.Note);
                        Row("Курс", _vm.Rate.ToString("0.00"));
                        Row("Сумма (сомони)", _vm.AmountTjs.ToString("N2"));
                        //Row("Сумма (евро)", _vm.AmountEuro.ToString("N2"));
                    });

                    content.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Text("Выдал ____________________");
                        row.RelativeItem().Text("Получил ____________________");
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Стр. ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }
    }
}