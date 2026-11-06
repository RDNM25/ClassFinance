<?php
require_once __DIR__ . '/../config/database.php';
require_once __DIR__ . '/../helpers/response.php';

try {
    $pdo = getDb();
    $pdo->query('SELECT 1');
    $tokensTable = $pdo->query("SHOW TABLES LIKE 'tokens'")->fetchColumn();
    if (!$tokensTable) {
        sendResponse(503, null, 'Import migration 007_api_tokens.sql in phpMyAdmin before signing in.');
    }
    sendResponse(200, ['service' => 'KasMateAPI', 'database' => 'connected']);
} catch (Throwable $exception) {
    error_log('KasMate API health check failed: ' . $exception->getMessage());
    sendResponse(503, null, 'The KasMate API cannot reach its database.');
}
