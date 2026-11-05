# Desktop MariaDB connection

The app connects directly to the local XAMPP MariaDB database `kasmate` on `127.0.0.1:3306`.
Start MySQL in the XAMPP Control Panel before opening the desktop app.

By default, the app uses XAMPP's common local credentials (`root` with a blank password).
If your MariaDB credentials differ, set these Windows environment variables before launching:

- `KASMATE_DB_HOST` (default `127.0.0.1`)
- `KASMATE_DB_PORT` (default `3306`)
- `KASMATE_DB_NAME` (default `kasmate`)
- `KASMATE_DB_USER` (default `root`)
- `KASMATE_DB_PASSWORD` (default blank)

Apply the numbered SQL files in order to an existing `kasmate` database. Migration
`004_bill_student_ledger.sql` adds an assessed amount per student/week and a read view that
reconstructs paid and remaining totals from linked payment transactions. Existing rows without
linked transactions keep their saved totals as a legacy fallback. The app creates the starter
class (`XI PPLG 2`, academic year `2025/2026`) if `classes` is empty, and
assigns unassigned imported students and transactions to the first class. User passwords are
verified as bcrypt hashes, matching the PHP API's `password_hash` values.

The app stores student, bill, bill-student, transaction, and notification data in MariaDB.
Weekly occurrences are bills with `weekly_schedule_id`, `period_start`, and `period_end`; each
student's entry points to that bill, and each payment transaction points to that entry. This
keeps payment history attached to the week it paid for. The app reads balance summaries from the
`bill_student_balances` view.
Migration `005_weekly_only_billing.sql` marks old one-off bills as archived while keeping their
records and payment history. Active billing screens and totals use scheduled weekly occurrences.
Legacy `payments` rows are left untouched; new payments are recorded as `transaksi` records.
Migration `006_transaction_payment_method.sql` adds the method (`TUNAI`, `QRIS`, or `TF`) to
transaction rows. Existing transactions default to `TUNAI` because their original method is not
recorded in the current schema.
