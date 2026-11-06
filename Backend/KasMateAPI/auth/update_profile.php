<?php
require_once __DIR__ . '/../config/database.php';
require_once __DIR__ . '/../helpers/response.php';
require_once __DIR__ . '/../middleware/auth.php';

try {
    $pdo = getDb();
    $user = requireAuth($pdo);
    requireRole($user, ['bendahara', 'wali_kelas', 'walikelas']);

    $input = readJsonInput();
    $currentPassword = (string) ($input['currentPassword'] ?? '');
    $username = trim((string) ($input['username'] ?? ''));
    $newPassword = (string) ($input['newPassword'] ?? '');
    $usernameLength = function_exists('mb_strlen') ? mb_strlen($username, 'UTF-8') : strlen($username);

    if ($usernameLength < 3 || $usernameLength > 100) {
        sendResponse(400, null, 'Username must be between 3 and 100 characters.');
    }
    if ($currentPassword === '' || !password_verify($currentPassword, (string) $user['password_hash'])) {
        sendResponse(403, null, 'The current password is incorrect.');
    }
    if ($newPassword !== '' && strlen($newPassword) < 8) {
        sendResponse(400, null, 'The new password must be at least 8 characters long.');
    }

    $existing = $pdo->prepare('SELECT id FROM users WHERE username = ? AND id <> ? LIMIT 1');
    $existing->execute([$username, (int) $user['id']]);
    if ($existing->fetchColumn() !== false) {
        sendResponse(409, null, 'That username is already in use.');
    }

    if ($newPassword !== '') {
        $statement = $pdo->prepare('UPDATE users SET username = ?, password_hash = ? WHERE id = ?');
        $statement->execute([$username, password_hash($newPassword, PASSWORD_DEFAULT), (int) $user['id']]);
    } else {
        $statement = $pdo->prepare('UPDATE users SET username = ? WHERE id = ?');
        $statement->execute([$username, (int) $user['id']]);
    }

    sendResponse(200, [
        'id' => (int) $user['id'],
        'name' => $user['name'],
        'username' => $username,
        'role' => $user['role'],
        'classId' => $user['class_id'] === null ? null : (int) $user['class_id'],
    ]);
} catch (PDOException $exception) {
    error_log('KasMate API profile update database error: ' . $exception->getMessage());
    sendResponse(503, null, 'The profile could not be updated because the database is unavailable.');
}
