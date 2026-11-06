# KasMate database and API

The desktop app communicates with MariaDB through the PHP API in `Backend/KasMateAPI`.
It no longer connects to MariaDB directly or stores database credentials.

## Run locally with XAMPP

1. Start **Apache** and **MySQL** in the XAMPP Control Panel.
2. Keep the API deployed at `D:\xampp\htdocs\KasMateAPI` (the source is in `Backend/KasMateAPI`).
3. In phpMyAdmin, import the numbered SQL files into the existing `kasmate` database. Migration
   `007_api_tokens.sql` adds the bearer-token table required for API sign-in.
4. Start Class Finance. The default API URL is `http://127.0.0.1/KasMateAPI/`.

If XAMPP or the database uses custom settings, configure the PHP environment variables
`KASMATE_DB_HOST`, `KASMATE_DB_PORT`, `KASMATE_DB_NAME`, `KASMATE_DB_USER`, and
`KASMATE_DB_PASSWORD` for Apache. The defaults are `127.0.0.1:3306`, database `kasmate`,
user `root`, and a blank password. To change the API address, set the Windows environment
variable `KASMATE_API_BASE_URL`, including the final slash (for example,
`http://192.168.1.20/KasMateAPI/` for a device on the same network).

## Data model

The API reads and writes students, classes, weekly schedules, weekly bills, per-student bill
balances, transactions, and notifications. Weekly occurrences are stored in `bills` with
`weekly_schedule_id`, `period_start`, and `period_end`; each student's entry is in
`bill_students`, and payments link through `transaksi.bill_student_id`. The API reads computed
amounts from the `bill_student_balances` view. Payment methods are `TUNAI`, `QRIS`, and `TF`.

Login is verified by PHP against the existing bcrypt `password_hash`. The API returns a bearer
token and never sends password hashes to the desktop. Bendahara accounts can see all classes;
Wali Kelas data is limited to the assigned class.

Migrations `004_bill_student_ledger.sql`, `005_weekly_only_billing.sql`, and
`006_transaction_payment_method.sql` add weekly balance tracking, archive old one-off bills,
and add transaction payment methods. `007_api_tokens.sql` adds API sessions. Existing legacy
`payments` rows are not modified.
