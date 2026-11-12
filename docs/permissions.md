# Bundled permissions

`Infrastructure/Constants/PermissionCatalog.cs` is the shared permission catalogue.
It is compiled into the app; the installer does not need a separate JSON file.

At startup, immediately after EF database migrations and before login,
`PermissionCatalogSynchronizer` adds missing permissions and updates descriptions.
It matches keys case-insensitively, normalizes known keys, and preserves existing IDs,
role assignments, and unknown permissions. The transaction rolls back on failure.
SQL Server update/range locks prevent overlapping synchronizations from inserting
the same permissions twice. Existing duplicate rows are retained to protect grants.

The permission catalogue screen is read-only. Role assignment checkboxes remain
editable. The catalogue synchronizer never grants new permissions to roles. In particular,
`OnlyOfficialCustomer` is a restriction, not an ordinary access grant.

## Add a permission

1. Add a constant and its Russian description to `PermissionCatalog.All`.
2. Use that constant in the appropriate permission check. An entry alone does not
   implement or enforce a feature.
3. Build and distribute the updated app. Each installation synchronizes its own
   database on startup, without internet access or manual permission SQL scripts.
4. Assign the new permission to the intended roles in the existing role editor.

Do not rename keys for cosmetic changes; edit descriptions instead. Removing an
entry from the catalogue does not delete it from existing databases. Retirement
or reassignment needs a deliberate data migration. Stores need the new app version
to receive new definitions; this is not a remote configuration service.

No new schema migration or manual `Update-Database` is needed for catalogue entries.
Existing pending schema migrations still run through the normal startup path.

## Administration tabs

`Admin` opens the Administration module; it does not bypass individual tab rights.
The existing `Admin.Brand` controls Brands and companies. Separate keys control
Managers (`Admin.Managers`), Coefficients (`Admin.Coefficients`), Bonuses
(`Admin.Bonuses`), Users (`Admin.Users`), References (`Admin.References`),
Database (`Admin.Database`) and Cleanup (`Admin.Cleanup`).

Reference links require both `Admin` and `Admin.References`, plus one of
`Admin.References.Organization`, `.Roles`, `.Couriers`, `.Pickers`, `.Managers`,
`.PriceLevels` or `.Currency`. The top-level Managers tab manages assignments;
the reference Managers directory remains read-only.

Tabs and links without their permissions are collapsed. Navigation commands check
the same permissions internally, including when invoked directly. Saving role
permissions refreshes the open menus and closes content whose grant was revoked.
Reopening Administration reloads current role grants from the database. There is
no bypass based on the role's name. Database editing/pasting is also guarded, and
CleanService requires Admin + Cleanup before opening a context and before commit.

One-time data migration `20261110030000_AddAdminTabPermissions` preserves the old
access of roles already assigned `Admin` by granting only newly introduced tab
and reference keys to those roles. It does not grant `Admin.Brand`, restore
already known unchecked keys, or grant anything to roles without `Admin`.
This keeps existing administrators able to reach the role editor after upgrading.
Subsequent migrations/catalogue synchronizations never restore revoked grants.
New roles receive only permissions explicitly assigned through the role editor.
The migration changes no business tables and its rollback retains role settings.

## Customer field permissions

`CustomerPermissionCatalog` adds 40 entries: View, Add, Edit, and Delete for each
of Debt, Restriction, BuyLimit, UnpaidPaymentLimit, Coefficients, ContractDate,
PriceLevel, SalesManager, Territory, and ExcludeDailyRepayment. Prefixes are
`Customers.<field>`; for example `Customers.BuyLimit.Edit`.

- View shows the field in the customer editor. Without it the field is hidden.
- Add fills an empty saved value. Edit changes a nonempty saved value.
- Delete clears/resets a field value, not the customer record. Users erase text
  directly; there are no separate clear buttons. Delete-only users can enter the
  field to erase its saved value, but saving a replacement still requires Edit.
  Temporarily clearing text before entering a replacement does not turn Edit into Add.
- Null, blank text, numeric zero, and false count as empty. Nonpositive limits
  disable their limit and therefore require Delete when replacing an active limit.
- The three coefficients share one permission group, but each changed value is
  checked individually. Recalculation checks all results before saving any of them;
  a result that resets a saved coefficient to zero requires Delete.
- The repayment-exclusion checkbox uses Add to turn on and Delete to turn off.
  Its Edit entry is included for consistency, but a boolean has no second nonempty value.
- Price level, manager, and territory remain required even with Delete. Creating
  a customer requires View and Add for all three, as well as the Customers module.
- Legacy `Customers.Debt` and `Customers.Restriction` grant View only, not writes.

Assign the new rights explicitly in the role editor, including for administrator
roles. Saving role permissions or changing a user's role reloads the signed-in
user's rights immediately. Opening Customers also reloads rights from the database,
so changes saved by another workstation apply on the next visit without logging out.
The open customer form and navigation menu react to changes in the permission cache.
If a refresh fails, stale grants are cleared and a warning is shown. Startup synchronization adds the
definitions but deliberately does not grant them to existing roles.

The customer save service validates changes against a freshly loaded database
record, not just UI state. Customer fields and manager links save in one transaction.
The single-customer coefficient calculation also uses a fresh context and does not
overwrite other unsaved form fields. These permissions cover the customer editor;
existing admin bulk coefficient operations and automatic order/payment calculations
remain separate workflows.

## Customer deletion

Client record operations are also limited by the manager scope described below;
the deletion grant alone does not expose other managers' clients.

`Customers.Delete` (Клиенты.Удаление клиента) is separate from field-clearing
permissions. Assign it explicitly to the intended roles together with `Customers`.
The styled Delete button appears in the Customers toolbar only with both grants
and is enabled only for a saved, selected client. It asks for confirmation with
the client's code and name. Permission-cache changes update the open toolbar.

CustomerService checks the current permission before accessing the database and
again before committing. It loads the saved client in a fresh transaction. Clients
with orders, payments, returns, monthly plans or product requests cannot be deleted;
the editor identifies the linked records. Successful deletion removes manager
assignments and clears the selected card. A failed deletion keeps the card and list.
No schema migration or automatic role grant is needed for this permission.

## Manager users and client scope

`Customers.ViewAll` (Клиенты.Просмотр всех клиентов), together with `Customers`,
allows access to all clients in the Clients editor. Grant it explicitly to the
intended roles, including administrators: startup adds the permission to the
catalogue without assigning it to any role.

Without ViewAll, an active manager user sees only customers whose
`Customers.SalesManagerId` points to that user's linked manager. Ordinary users
without ViewAll see an empty list. The existing official-client-only filter still
applies when enabled. The manager selector and creation, editing, deletion and
individual coefficient calculation enforce the same scope. Unscoped sales,
reporting and administrator assignment queries retain their existing behavior.

Migration `20261110020000_LinkUserManagers` adds `Users.IsManager`, `Users.Address`
and a nullable, unique `SalesManagers.UserId`. It preserves existing managers and
their assignments. Enabling «Менеджер» and saving a user automatically creates
that user's manager record if none is linked yet. The user form has no manager
selector, and existing unrelated records are never linked by matching names.

UserService saves the user, role and manager profile in one transaction. Name,
address and phone synchronize to manager name, address and contacts. Removing the Manager
flag deactivates the same record; re-enabling restores it and its assignments.
The Managers directory is read-only, and assignment screens cannot edit profile
fields. Deleting a user preserves an inactive, detached manager and is blocked
when that user has sales history.

Permission-cache changes clear and reload an open client editor. Each service
operation uses a fresh context and current user-to-manager linkage, so changed
assignments or the Manager flag do not rely on an old entity cached at login.

## Order options

`Orders.ViewAllManagers` (Заказы.Просмотр всех менеджеров), together with `Orders`,
allows choosing any active manager in the order form. It is added to the catalogue
without automatic grants, including to administrator roles. Without this grant,
the selector is locked and contains only the active manager linked through
`SalesManagers.UserId` to the logged-in user. Names and login text are not used
to match manager records. The user's own manager remains the initial choice even
when the all-manager permission is present.

RetailManagerService loads a fresh scoped manager/customer snapshot. Clients,
routes, required/planned purchase panels and their counts follow the selected
manager. A manager change clears an incompatible client, cart and delivery
selection. Reset/reopening restores the current user's own manager. A revoked
all-manager grant immediately restores the fixed own scope; logout/account changes
clear old data, and delayed responses cannot replace a newer user or filter.
Before checkout, the form also validates current customer access against the database.

For a seller without an active manager, existing `Customers.ViewAll` access still
allows the full client list; it does not unlock the manager selector. Without
either an active own manager or a full-client/all-manager grant, the order form
shows no clients. The Clients editor retains its independent access policy.
Monthly-plan totals retain their existing permissions and global calculation.
The existing UserId database relation is reused; this feature adds no schema fields.

`Orders.Barter` (Заказы.Бартер) and `Orders.DirectFromStock`
(Заказы.Заказ прямо со склада) are separate permissions. Startup synchronization
adds them without granting either to existing roles. Assign each explicitly in
the role editor, including for administrator roles.

Without the corresponding permission, the order checkbox and its label are hidden. The open
order form reacts to permission-cache changes and unchecks a mode when its grant
is revoked. OrderService also rejects unauthorized modes before database access.
An order using both options requires both permissions; ordinary orders require
neither of these additional grants.

`Orders.ExceedBuyLimit` (Заказы.Продажа сверх лимита суммы заказа) permits selling
above the customer's order amount limit, including the prepayment-adjusted basket
allowance. It bypasses amount checks when adding products, changing quantities,
and saving an order, without changing the customer's saved limit. The current
permission cache is consulted for each check, so revocation restores enforcement.
It does not bypass the separate unpaid-payment limit, stock checks, required
fields, or the permissions for barter and direct-stock orders. Assign this right
explicitly; startup synchronization does not grant it automatically.

## Verification

Run `dotnet run --project tests/PermissionCatalog.Tests/PermissionCatalog.Tests.csproj`.
This standalone harness references the production Infrastructure project without
building or launching WPF. It uses SQL Server LocalDB and a uniquely
named temporary test database, never the application's MDF. The database is dropped
after the tests; its files are placed in the test project's ignored build output.

Checks cover catalogue validity, clean installs, repeated startup, upgrades and role
preservation, legacy casing and duplicates, concurrent startup, and transaction rollback.
It also tests field permissions, required selections, denied saves, stale contexts,
manager-link consistency, and atomic coefficient calculations. Order option checks
cover all 16 combinations of the two grants and two flags, plus revocation on an
existing service instance.

Run `dotnet run --project tests/CustomerView.Tests/CustomerView.Tests.csproj` on Windows
for the customer-form binding checks. This loads only the form and its view model,
without application startup, migrations, a visible window, or a database connection.
It checks hidden/read-only fields, clearing, required selections, and checkbox/input bindings.
It also loads the permission catalogue and verifies read-only entries and automatic
row sizing for wrapped descriptions, catching runtime XAML conversion errors.
