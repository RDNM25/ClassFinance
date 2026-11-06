<?php
require '../config/database.php';
require '../helpers/response.php';
require '../middleware/auth.php';

$pdo = getDb();
$user = requireAuth($pdo);

$input = json_decode(file_get_contents('php://input'), true);
$studentId = $input['student_id'] ?? null;
$amount = (int) ($input['amount'] ?? 0);

if (!$studentId || $amount <= 0) {
    sendResponse(400, null, 'student_id and positive amount required');
}

$pdo->beginTransaction();
try {
    $stmt = $pdo->prepare('SELECT * FROM students WHERE id = ? FOR UPDATE');
    $stmt->execute([$studentId]);
    $student = $stmt->fetch();

    if (!$student) {
        $pdo->rollBack();
        sendResponse(404, null, 'Student not found');
    }

    $newOwed = max(0, $student['owed'] - $amount);
    $pdo->prepare('UPDATE students SET owed = ? WHERE id = ?')
        ->execute([$newOwed, $studentId]);

    $pdo->prepare(
        'INSERT INTO payments (student_id, amount, paid_at) VALUES (?, ?, NOW())'
    )->execute([$studentId, $amount]);

    $pdo->prepare(
        "INSERT INTO transaksi (type, amount, note, student_id, created_at)
        VALUES ('MASUK', ?, ?, ?, NOW())"
    )->execute([$amount, "Pembayaran dari {$student['nama']}", $studentId]);

    $pdo->commit();
    sendResponse(200, ['student_id' => $studentId, 'new_owed' => $newOwed]);
} catch (Exception $e) {
    $pdo->rollBack();
    sendResponse(500, null, 'Transaction failed');
}