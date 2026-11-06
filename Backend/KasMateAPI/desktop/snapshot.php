<?php
require_once __DIR__ . '/../config/database.php';
require_once __DIR__ . '/../helpers/response.php';
require_once __DIR__ . '/../middleware/auth.php';

try {
    $pdo = getDb();
    $user = requireAuth($pdo);
    $role = strtolower((string) ($user['role'] ?? ''));
    if (!in_array($role, ['bendahara', 'wali_kelas', 'walikelas'], true)) {
        sendResponse(403, null, 'This account cannot access the desktop data.');
    }
    $isTreasurer = $role === 'bendahara';
    $classId = $isTreasurer ? null : (isset($user['class_id']) ? (int) $user['class_id'] : null);
    if (!$isTreasurer && $classId === null) {
        sendResponse(403, null, 'This account is not assigned to a class.');
    }

    if ((int) $pdo->query('SELECT COUNT(*) FROM classes')->fetchColumn() === 0) {
        $pdo->prepare('INSERT INTO classes (name, academic_year) VALUES (?, ?)')
            ->execute(['XI PPLG 2', '2025/2026']);
    }
    $defaultClassId = (int) $pdo->query('SELECT id FROM classes ORDER BY id LIMIT 1')->fetchColumn();
    $pdo->prepare('UPDATE students SET class_id = ? WHERE class_id IS NULL')->execute([$defaultClassId]);
    $pdo->prepare('UPDATE transaksi SET class_id = ? WHERE class_id IS NULL')->execute([$defaultClassId]);

    $queryRows = static function (string $sql, array $params = []) use ($pdo): array {
        $statement = $pdo->prepare($sql);
        $statement->execute($params);
        return $statement->fetchAll();
    };
    $classClause = $isTreasurer ? '' : ' WHERE id = ?';
    $classParams = $isTreasurer ? [] : [$classId];
    $classes = array_map(static fn(array $row): array => [
        'id' => (int) $row['id'],
        'name' => $row['name'],
        'academicYear' => $row['academic_year'],
    ], $queryRows('SELECT id, name, academic_year FROM classes' . $classClause . ' ORDER BY id', $classParams));
    $classFilter = $isTreasurer ? '' : ' WHERE class_id = ?';
    $classFilterParams = $isTreasurer ? [] : [$classId];

    $schedules = array_map(static fn(array $row): array => [
        'id' => (int) $row['id'],
        'classId' => (int) $row['class_id'],
        'name' => $row['name'],
        'amount' => $row['amount'],
        'startsOn' => $row['starts_on'],
        'endsOn' => $row['ends_on'],
        'isActive' => (bool) $row['is_active'],
        'createdAt' => isoDateTime($row['created_at']),
    ], $queryRows('SELECT id, class_id, name, amount, starts_on, ends_on, is_active, created_at FROM weekly_billing_schedules' . $classFilter . ' ORDER BY id', $classFilterParams));

    // Password hashes never leave the API. Desktop login is verified by auth/login.php.
    $staff = array_map(static fn(array $row): array => [
        'id' => (int) $row['id'],
        'name' => $row['name'],
        'username' => $row['username'],
        'role' => str_replace('_', '', strtolower((string) $row['role'])) === 'walikelas' ? 'WaliKelas' : 'Bendahara',
        'classId' => $row['class_id'] === null ? null : (int) $row['class_id'],
    ], $queryRows('SELECT id, name, username, role, class_id FROM users WHERE id = ' . (int) $user['id'] . ' ORDER BY id'));

    $students = array_map(static fn(array $row): array => [
        'id' => (int) $row['id'],
        'name' => $row['nama'],
        'nis' => $row['nis'] ?? '',
        'classId' => $row['class_id'] === null ? $defaultClassId : (int) $row['class_id'],
        'balance' => $row['saldo_titipan'],
    ], $queryRows('SELECT id, nama, nis, class_id, saldo_titipan FROM students' . $classFilter . ' ORDER BY id', $classFilterParams));

    $bills = array_map(static fn(array $row): array => [
        'id' => (int) $row['id'],
        'classId' => (int) $row['class_id'],
        'name' => $row['name'],
        'amount' => $row['amount'],
        'createdAt' => isoDateTime($row['created_at']),
        'scheduleId' => $row['weekly_schedule_id'] === null ? null : (int) $row['weekly_schedule_id'],
        'periodStart' => $row['period_start'],
        'periodEnd' => $row['period_end'],
        'isArchived' => (bool) $row['is_archived'],
    ], $queryRows('SELECT id, class_id, name, amount, created_at, weekly_schedule_id, period_start, period_end, is_archived FROM bills' . $classFilter . ' ORDER BY id', $classFilterParams));

    $billStudents = array_map(static function (array $row): array {
        $status = strtoupper((string) $row['status']);
        return [
            'id' => (int) $row['id'],
            'billId' => (int) $row['bill_id'],
            'studentId' => (int) $row['student_id'],
            'status' => match ($status) {
                'SEBAGIAN' => 'Sebagian',
                'PROSES' => 'Proses',
                'LUNAS' => 'Lunas',
                default => 'BelumBayar',
            },
            'amountDue' => $row['amount_due'],
            'amountPaid' => $row['amount_paid'],
            'assessedAmount' => $row['assessed_amount'],
        ];
    }, $queryRows('SELECT bsb.id, bsb.bill_id, bsb.student_id, bsb.status, bsb.amount_due, bsb.amount_paid, bsb.assessed_amount
        FROM bill_student_balances bsb JOIN bills b ON b.id = bsb.bill_id' . ($isTreasurer ? '' : ' WHERE b.class_id = ?') . ' ORDER BY bsb.id', $classFilterParams));

    $transactions = array_map(static function (array $row) use ($defaultClassId): array {
        $method = strtoupper((string) $row['payment_method']);
        return [
            'id' => (int) $row['id'],
            'classId' => $row['class_id'] === null ? $defaultClassId : (int) $row['class_id'],
            'type' => strtoupper((string) $row['type']) === 'MASUK' ? 'Masuk' : 'Keluar',
            'method' => match ($method) {
                'QRIS' => 'Qris',
                'TF', 'TRANSFER' => 'Tf',
                default => 'Tunai',
            },
            'amount' => $row['amount'],
            'description' => $row['note'] ?? '',
            'studentId' => $row['student_id'] === null ? null : (int) $row['student_id'],
            'billStudentId' => $row['bill_student_id'] === null ? null : (int) $row['bill_student_id'],
            'createdBy' => $row['created_by'] ?? '',
            'createdAt' => isoDateTime($row['created_at']),
            'affectsKas' => (bool) $row['affects_kas'],
            'isRefund' => (bool) $row['is_refund'],
            'isPendingRefund' => (bool) $row['is_pending_refund'],
        ];
    }, $queryRows('SELECT id, class_id, type, payment_method, amount, note, student_id, bill_student_id, created_by, created_at, affects_kas, is_refund, is_pending_refund FROM transaksi' . $classFilter . ' ORDER BY id', $classFilterParams));

    $notifications = array_map(static fn(array $row): array => [
        'id' => (int) $row['id'],
        'studentId' => (int) $row['student_id'],
        'billId' => $row['bill_id'] === null ? null : (int) $row['bill_id'],
        'title' => $row['title'],
        'message' => $row['message'],
        'isRead' => (bool) $row['is_read'],
        'createdAt' => isoDateTime($row['created_at']),
    ], $queryRows('SELECT n.id, n.student_id, n.bill_id, n.title, n.message, n.is_read, n.created_at
        FROM notifications n JOIN students s ON s.id = n.student_id' . ($isTreasurer ? '' : ' WHERE s.class_id = ?') . ' ORDER BY n.id', $classFilterParams));

    sendResponse(200, [
        'idCounters' => [
            'maxUserId' => max((int) $pdo->query('SELECT COALESCE(MAX(id), 0) FROM users')->fetchColumn(),
                (int) $pdo->query('SELECT COALESCE(MAX(id), 0) FROM students')->fetchColumn()),
            'maxClassId' => (int) $pdo->query('SELECT COALESCE(MAX(id), 0) FROM classes')->fetchColumn(),
            'maxScheduleId' => (int) $pdo->query('SELECT COALESCE(MAX(id), 0) FROM weekly_billing_schedules')->fetchColumn(),
            'maxBillId' => (int) $pdo->query('SELECT COALESCE(MAX(id), 0) FROM bills')->fetchColumn(),
            'maxBillStudentId' => (int) $pdo->query('SELECT COALESCE(MAX(id), 0) FROM bill_students')->fetchColumn(),
            'maxTransactionId' => (int) $pdo->query('SELECT COALESCE(MAX(id), 0) FROM transaksi')->fetchColumn(),
            'maxNotificationId' => (int) $pdo->query('SELECT COALESCE(MAX(id), 0) FROM notifications')->fetchColumn(),
        ],
        'classes' => $classes,
        'schedules' => $schedules,
        'staff' => $staff,
        'students' => $students,
        'bills' => $bills,
        'billStudents' => $billStudents,
        'transactions' => $transactions,
        'notifications' => $notifications,
    ]);
} catch (PDOException $exception) {
    error_log('KasMate API snapshot database error: ' . $exception->getMessage());
    sendResponse(500, null, 'The application data could not be loaded.');
}
