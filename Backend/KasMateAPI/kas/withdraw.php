<?php
require '../config/database.php';
require '../helpers/response.php';
require '../middleware/auth.php';

$pdo = getDb();
$user = requireAuth($pdo);

$input = json_decode(file_get_contents('php://input'), true);
$amount = (int) ($input['amount'] ?? 0);
$note = trim($input['note'] ?? '');

if ($amount <= 0) {
    sendResponse(400, null, 'Positive amount required');
}

$stmt = $pdo->query(
    "SELECT COALESCE(SUM(CASE WHEN type='MASUK' THEN amount ELSE -amount END), 0) AS saldo
    FROM transaksi"
);
$saldo = (int) $stmt->fetch()['saldo'];

if ($amount > $saldo) {
    sendResponse(400, null, 'Insufficient balance');
}

$pdo->prepare(
    "INSERT INTO transaksi (type, amount, note, created_at) VALUES ('KELUAR', ?, ?, NOW())"
)->execute([$amount, $note]);

sendResponse(200, ['withdrawn' => $amount, 'remaining' => $saldo - $amount]);