<?php
require_once __DIR__ . '/../config/database.php';
require_once __DIR__ . '/../helpers/response.php';

try {
    $pdo = getDb();
    $headers = function_exists('getallheaders') ? getallheaders() : [];
    $authHeader = $_SERVER['HTTP_AUTHORIZATION'] ?? '';
    foreach ($headers as $name => $value) {
        if (strtolower($name) === 'authorization') $authHeader = $value;
    }

    if (str_starts_with($authHeader, 'Bearer ')) {
        $token = substr($authHeader, 7);
        $pdo->prepare('DELETE FROM tokens WHERE token = ?')->execute([$token]);
    }

    sendResponse(200, ['message' => 'Logged out.']);
} catch (PDOException $exception) {
    error_log('KasMate API logout database error: ' . $exception->getMessage());
    sendResponse(503, null, 'The KasMate database is unavailable.');
}
