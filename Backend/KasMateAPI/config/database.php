<?php
function getDb(): PDO
{
    $host = getenv('KASMATE_DB_HOST') ?: '127.0.0.1';
    $port = (int) (getenv('KASMATE_DB_PORT') ?: 3306);
    $dbname = getenv('KASMATE_DB_NAME') ?: 'kasmate';
    $user = getenv('KASMATE_DB_USER') ?: 'root';
    $pass = getenv('KASMATE_DB_PASSWORD') ?: '';

    $pdo = new PDO(
        "mysql:host=$host;port=$port;dbname=$dbname;charset=utf8mb4",
        $user,
        $pass,
        [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            PDO::ATTR_EMULATE_PREPARES => false,
        ]
    );
    return $pdo;
}
