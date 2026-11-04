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

The database must have the migration in `001_add_classfinance_schema.sql` applied. The app
creates the starter class (`XI PPLG 2`, academic year `2025/2026`) if `classes` is empty, and
assigns unassigned imported students and transactions to the first class. User passwords are
verified as bcrypt hashes, matching the PHP API's `password_hash` values.

The app stores student, bill, bill-student, transaction, and notification data in MariaDB.
Legacy `payments` rows are left untouched; new payments are recorded as `transaksi` records.
