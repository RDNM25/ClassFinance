<?php
require '../config/database.php';
require '../helpers/response.php';
require '../middleware/auth.php';

$pdo = getDb();
requireAuth($pdo);

$input = json_decode(file_get_contents('php://input'), true);
$nama = trim($input['nama'] ?? '');
$owed = (int) ($input['owed'] ?? 0);

if ($nama === '') {
    sendResponse(400, null, 'Student name is required');
}
if ($owed < 0) {
    sendResponse(400, null, 'Owed amount cannot be negative');
}

$stmt = $pdo->prepare('INSERT INTO students (nama, owed) VALUES (?, ?)');
$stmt->execute([$nama, $owed]);

sendResponse(201, [
    'id' => $pdo->lastInsertId(),
    'nama' => $nama,
    'owed' => $owed,
]);