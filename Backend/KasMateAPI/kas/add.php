<?php
require '../config/database.php';
require '../helpers/response.php';
require '../middleware/auth.php';

$pdo = getDb();
requireAuth($pdo);

$input = json_decode(file_get_contents('php://input'), true);
$amount = (int) ($input['amount'] ?? 0);
$note = trim($input['note'] ?? '');

if ($amount <= 0) {
    sendResponse(400, null, 'Positive amount required');
}

$pdo->prepare(
    "INSERT INTO transaksi (type, amount, note, created_at) VALUES ('MASUK', ?, ?, NOW())"
)->execute([$amount, $note]);

$stmt = $pdo->query(
    "SELECT COALESCE(SUM(CASE WHEN type='MASUK' THEN amount ELSE -amount END), 0) AS saldo
     FROM transaksi"
);
sendResponse(200, ['added' => $amount, 'saldo' => (int) $stmt->fetch()['saldo']]);