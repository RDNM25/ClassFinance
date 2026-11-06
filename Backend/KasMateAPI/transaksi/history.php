<?php
require '../config/database.php';
require '../helpers/response.php';
require '../middleware/auth.php';

$pdo = getDb();
requireAuth($pdo);

$stmt = $pdo->query(
    "SELECT t.id, t.type, t.amount, t.note, t.student_id, t.created_at,
            s.nama AS student_nama
    FROM transaksi t
    LEFT JOIN students s ON s.id = t.student_id
    ORDER BY t.created_at DESC"
);
sendResponse(200, $stmt->fetchAll());