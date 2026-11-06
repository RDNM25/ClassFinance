<?php
require '../config/database.php';
require '../helpers/response.php';
require '../middleware/auth.php';

$pdo = getDb();
requireAuth($pdo);

$id = $_GET['id'] ?? null;
if (!$id) {
    sendResponse(400, null, 'id is required');
}

$stmt = $pdo->prepare('SELECT id, nama, owed FROM students WHERE id = ?');
$stmt->execute([$id]);
$student = $stmt->fetch();

if (!$student) {
    sendResponse(404, null, 'Student not found');
}

$stmt = $pdo->prepare(
    'SELECT id, amount, paid_at FROM payments WHERE student_id = ? ORDER BY paid_at DESC'
);
$stmt->execute([$id]);
$student['payments'] = $stmt->fetchAll();

sendResponse(200, $student);