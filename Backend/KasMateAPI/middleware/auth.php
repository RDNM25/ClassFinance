<?php
function requireAuth(PDO $pdo): array
{
    $headers = function_exists('getallheaders') ? getallheaders() : [];
    $authHeader = '';
    foreach ($headers as $name => $value) {
        if (strtolower($name) === 'authorization') {
            $authHeader = $value;
            break;
        }
    }
    $authHeader = $authHeader ?: ($_SERVER['HTTP_AUTHORIZATION'] ?? '');

    if (!str_starts_with($authHeader, 'Bearer ')) {
        sendResponse(401, null, 'Missing access token.');
    }

    $token = substr($authHeader, 7);
    $stmt = $pdo->prepare(
        'SELECT u.* FROM users u JOIN tokens t ON t.user_id = u.id
        WHERE t.token = ? AND t.expires_at > NOW()'
    );
    $stmt->execute([$token]);
    $user = $stmt->fetch();

    if (!$user) {
        sendResponse(401, null, 'Invalid or expired access token.');
    }
    return $user;
}

function requireRole(array $user, array $roles): void
{
    if (!in_array(strtolower((string) ($user['role'] ?? '')), $roles, true)) {
        sendResponse(403, null, 'You do not have permission to perform this action.');
    }
}
