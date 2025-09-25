using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Entities;

public class OrganizationInfoEntity
{
    [Key]
    public string OrganizationCode { get; set; } =null!;                 // КодОрганизации
    public string Name { get; set; } = null!;                              // НазваниеКомпании
    public string? Address { get; set; }                                   // Адрес
    public string? City { get; set; }                                     // Город
    public string? Region { get; set; }                                 // Область/Республика
    public string? PhoneNumber { get; set; }                            // НомерТелефона
    public string? ExportPath { get; set; }                               // путь для экспорта данных
    public string? ImportPath { get; set; }                             // путь для импорта товаров
}
