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
editable. New permissions are not automatically granted to any role. In particular,
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

## Order options

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
