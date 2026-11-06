# KasMateAPI

This PHP REST API is the server-side data layer for Class Finance. The XAMPP deployment lives
at `D:\xampp\htdocs\KasMateAPI`; this folder is the versioned source in the repository.

## Desktop endpoints

- `GET desktop/health.php` checks that the API and MariaDB are available.
- `POST auth/login.php` verifies the existing bcrypt account and issues a bearer token.
- `POST auth/update_profile.php` verifies the current password before changing a staff username or password.
- `GET desktop/snapshot.php` returns the signed-in user's class data.
- `POST desktop/sync.php` saves a batch of desktop changes in one MariaDB transaction.
- `POST auth/logout.php` revokes the current token.

Responses use `{ "success": true, "data": ..., "error": null }`. Queries use PDO prepared
statements. Password hashes stay on the PHP host. Bendahara can access all classes; Wali Kelas
data is limited to the assigned class.

## Configuration

XAMPP defaults to `127.0.0.1:3306`, database `kasmate`, user `root`, and a blank password.
Apache can override these with `KASMATE_DB_HOST`, `KASMATE_DB_PORT`, `KASMATE_DB_NAME`,
`KASMATE_DB_USER`, and `KASMATE_DB_PASSWORD`. Set `KASMATE_CORS_ORIGINS` to a comma-separated
list of exact web origins if a browser-based client needs cross-origin access. Native clients
do not need CORS.

The desktop app defaults to `http://127.0.0.1/KasMateAPI/`; set Windows environment variable
`KASMATE_API_BASE_URL` to use another host, such as the XAMPP machine's LAN address.

Apply `ClassFinance/Database/007_api_tokens.sql` once if the `tokens` table does not yet exist.
