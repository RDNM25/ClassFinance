<?php
require_once __DIR__ . '/../config/database.php';
require_once __DIR__ . '/../helpers/response.php';

$input = readJsonInput();
$username = trim($input['username'] ?? '');
$password = $input['password'] ?? '';

if ($username === '' || $password === '') {
    sendResponse(400, null, 'Username and password are required.');
}

try {
    $pdo = getDb();
    $stmt = $pdo->prepare('SELECT id, name, username, password_hash, role, class_id FROM users WHERE username = ? LIMIT 1');
    $stmt->execute([$username]);
    $user = $stmt->fetch();

    if (!$user || !password_verify($password, $user['password_hash'])) {
        sendResponse(401, null, 'Username or password is incorrect.');
    }

    // Remove expired sessions for this account, then issue a fresh bearer token.
    $pdo->prepare('DELETE FROM tokens WHERE user_id = ? AND expires_at < NOW()')
        ->execute([$user['id']]);

    $token = bin2hex(random_bytes(32));
    $expiresAt = date('Y-m-d H:i:s', strtotime('+30 days'));

    $pdo->prepare(
        'INSERT INTO tokens (user_id, token, expires_at) VALUES (?, ?, ?)'
    )->execute([$user['id'], $token, $expiresAt]);

    sendResponse(200, [
        'token' => $token,
        'id' => (int) $user['id'],
        'name' => $user['name'],
        'username' => $user['username'],
        'role' => $user['role'],
        'classId' => $user['class_id'] === null ? null : (int) $user['class_id'],
        'user' => [
            'id' => (int) $user['id'],
            'name' => $user['name'],
            'username' => $user['username'],
            'role' => $user['role'],
            'classId' => $user['class_id'] === null ? null : (int) $user['class_id'],
        ],
    ]);
} catch (PDOException $exception) {
    error_log('KasMate API login database error: ' . $exception->getMessage());
    sendResponse(503, null, 'The KasMate database is unavailable.');
}
