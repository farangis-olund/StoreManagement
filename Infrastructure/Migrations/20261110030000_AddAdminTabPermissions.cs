using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261110030000_AddAdminTabPermissions")]
public sealed class AddAdminTabPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Split previously unrestricted Admin access once. Existing explicit choices, especially
        // Admin.Brand, are untouched. Later startups must never restore a revoked tab permission.
        migrationBuilder.Sql("""
            DECLARE @definitions TABLE ([Key] nvarchar(128), [Description] nvarchar(256));
            INSERT INTO @definitions VALUES
                (N'Admin.Managers', N'Администрация.Менеджеры'),
                (N'Admin.Coefficients', N'Администрация.Коэффициенты'),
                (N'Admin.Bonuses', N'Администрация.Бонусы'),
                (N'Admin.Users', N'Администрация.Пользователи'),
                (N'Admin.References', N'Администрация.Справочники'),
                (N'Admin.Database', N'Администрация.База данных'),
                (N'Admin.Cleanup', N'Администрация.Очистка базы'),
                (N'Admin.References.Organization', N'Администрация.Справочники.Организация'),
                (N'Admin.References.Roles', N'Администрация.Справочники.Управление ролями'),
                (N'Admin.References.Couriers', N'Администрация.Справочники.Доставщики'),
                (N'Admin.References.Pickers', N'Администрация.Справочники.Сборщики'),
                (N'Admin.References.Managers', N'Администрация.Справочники.Менеджеры'),
                (N'Admin.References.PriceLevels', N'Администрация.Справочники.Уровни цен клиента'),
                (N'Admin.References.Currency', N'Администрация.Справочники.Назначение валюты');

            DECLARE @added TABLE ([Id] int, [Key] nvarchar(128));
            INSERT INTO [dbo].[Permissions] ([Key], [Description])
                OUTPUT INSERTED.[Id], INSERTED.[Key] INTO @added
                SELECT d.[Key], d.[Description] FROM @definitions d
                WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] p WITH (UPDLOCK, HOLDLOCK)
                    WHERE UPPER(p.[Key]) = UPPER(d.[Key]));

            INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
                SELECT DISTINCT rp.[RoleId], a.[Id]
                FROM [dbo].[RolePermissions] rp
                INNER JOIN [dbo].[Permissions] p ON p.[Id] = rp.[PermissionId]
                CROSS JOIN @added a
                WHERE UPPER(p.[Key]) = N'ADMIN'
                    AND NOT EXISTS (SELECT 1 FROM [dbo].[RolePermissions] existing WITH (UPDLOCK, HOLDLOCK)
                        WHERE existing.[RoleId] = rp.[RoleId] AND existing.[PermissionId] = a.[Id]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain role settings: a rollback must not delete permissions or administrator choices.
    }
}
