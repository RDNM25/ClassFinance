<?php
require '../config/database.php';
require '../helpers/response.php';
require '../middleware/auth.php';

$pdo = getDb();
requireAuth($pdo);

$stmt = $pdo->query(
    "SELECT COALESCE(SUM(CASE WHEN type='MASUK' THEN amount ELSE -amount END), 0) AS saldo
    FROM transaksi"
);
sendResponse(200, ['saldo' => (int) $stmt->fetch()['saldo']]);