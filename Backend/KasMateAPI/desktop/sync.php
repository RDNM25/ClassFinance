<?php
require_once __DIR__ . '/../config/database.php';
require_once __DIR__ . '/../helpers/response.php';
require_once __DIR__ . '/../middleware/auth.php';

function rowsFor(array $input, string $table, string $kind): array
{
    $rows = $input[$table][$kind] ?? [];
    if (!is_array($rows)) sendResponse(400, null, "Invalid $table changes.");
    return $rows;
}

function sqlDate($value): ?string
{
    if ($value === null || $value === '') return null;
    $timestamp = strtotime((string) $value);
    return $timestamp === false ? null : date('Y-m-d', $timestamp);
}

function sqlDateTime($value): ?string
{
    if ($value === null || $value === '') return null;
    $timestamp = strtotime((string) $value);
    return $timestamp === false ? null : date('Y-m-d H:i:s', $timestamp);
}

function enumValue($value): string
{
    return strtolower(str_replace(['_', '-', ' '], '', (string) $value));
}

function statusForDb($value): string
{
    return match (enumValue($value)) {
        'sebagian' => 'SEBAGIAN',
        'proses' => 'PROSES',
        'lunas' => 'LUNAS',
        default => 'BELUM_BAYAR',
    };
}

function methodForDb($value): string
{
    return match (enumValue($value)) {
        'qris' => 'QRIS',
        'tf', 'transfer' => 'TF',
        default => 'TUNAI',
    };
}

function assertClassAccess(array $row, bool $isTreasurer, ?int $classId): void
{
    if ($isTreasurer) return;
    if ($classId === null || (int) ($row['classId'] ?? 0) !== $classId) {
        sendResponse(403, null, 'You cannot change data outside your assigned class.');
    }
}

try {
    $pdo = getDb();
    $user = requireAuth($pdo);
    $role = strtolower((string) ($user['role'] ?? ''));
    $isTreasurer = $role === 'bendahara';
    $isTeacher = in_array($role, ['wali_kelas', 'walikelas'], true);
    if (!$isTreasurer && !$isTeacher) {
        sendResponse(403, null, 'This account cannot save desktop data.');
    }
    $classId = isset($user['class_id']) ? (int) $user['class_id'] : null;
    if ($isTeacher && $classId === null) {
        sendResponse(403, null, 'This account is not assigned to a class.');
    }

    $input = readJsonInput();
    $classes = rowsFor($input, 'classes', 'upserts');
    $scheduleUpserts = rowsFor($input, 'schedules', 'upserts');
    $staffUpserts = rowsFor($input, 'staff', 'upserts');
    $studentUpserts = rowsFor($input, 'students', 'upserts');
    $billUpserts = rowsFor($input, 'bills', 'upserts');
    $billStudentUpserts = rowsFor($input, 'billStudents', 'upserts');
    $transactionUpserts = rowsFor($input, 'transactions', 'upserts');
    $notificationUpserts = rowsFor($input, 'notifications', 'upserts');

    if (!$isTreasurer && ($classes || rowsFor($input, 'classes', 'deletes') || $scheduleUpserts ||
        rowsFor($input, 'schedules', 'deletes') || $staffUpserts || rowsFor($input, 'staff', 'deletes') ||
        $billUpserts || rowsFor($input, 'bills', 'deletes') || $billStudentUpserts ||
        $notificationUpserts)) {
        sendResponse(403, null, 'This account can only manage students in its assigned class.');
    }

    foreach ($studentUpserts as $row) assertClassAccess($row, $isTreasurer, $classId);
    foreach ($transactionUpserts as $row) {
        assertClassAccess($row, $isTreasurer, $classId);
    }

    $pdo->beginTransaction();

    foreach ($studentUpserts as $row) {
        $statement = $pdo->prepare(
            'INSERT INTO students (id, nama, nis, class_id, saldo_titipan) VALUES (?, ?, ?, ?, ?)
             ON DUPLICATE KEY UPDATE nama=VALUES(nama), nis=VALUES(nis), class_id=VALUES(class_id), saldo_titipan=VALUES(saldo_titipan)'
        );
        $statement->execute([(int) $row['id'], (string) ($row['name'] ?? ''), ($row['nis'] ?? '') ?: null,
            (int) $row['classId'], (string) ($row['balance'] ?? 0)]);
    }

    if ($isTreasurer) {
        foreach ($scheduleUpserts as $row) {
            $statement = $pdo->prepare(
                'INSERT INTO weekly_billing_schedules (id, class_id, name, amount, starts_on, ends_on, is_active, created_at)
                 VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                 ON DUPLICATE KEY UPDATE class_id=VALUES(class_id), name=VALUES(name), amount=VALUES(amount), starts_on=VALUES(starts_on), ends_on=VALUES(ends_on), is_active=VALUES(is_active)'
            );
            $statement->execute([(int) $row['id'], (int) $row['classId'], (string) $row['name'], (string) $row['amount'],
                sqlDate($row['startsOn'] ?? null), sqlDate($row['endsOn'] ?? null), !empty($row['isActive']) ? 1 : 0,
                sqlDateTime($row['createdAt'] ?? null) ?? date('Y-m-d H:i:s')]);
        }

        foreach ($billUpserts as $row) {
            $statement = $pdo->prepare(
                'INSERT INTO bills (id, class_id, name, amount, created_at, weekly_schedule_id, period_start, period_end, is_archived)
                 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                 ON DUPLICATE KEY UPDATE class_id=VALUES(class_id), name=VALUES(name), amount=VALUES(amount), created_at=VALUES(created_at), weekly_schedule_id=VALUES(weekly_schedule_id), period_start=VALUES(period_start), period_end=VALUES(period_end), is_archived=VALUES(is_archived)'
            );
            $statement->execute([(int) $row['id'], (int) $row['classId'], (string) $row['name'], (string) $row['amount'],
                sqlDateTime($row['createdAt'] ?? null) ?? date('Y-m-d H:i:s'), $row['scheduleId'] ?? null,
                sqlDate($row['periodStart'] ?? null), sqlDate($row['periodEnd'] ?? null), !empty($row['isArchived']) ? 1 : 0]);
        }

        foreach ($billStudentUpserts as $row) {
            $statement = $pdo->prepare(
                'INSERT INTO bill_students (id, bill_id, student_id, status, amount_due, amount_paid, assessed_amount)
                 VALUES (?, ?, ?, ?, ?, ?, ?)
                 ON DUPLICATE KEY UPDATE bill_id=VALUES(bill_id), student_id=VALUES(student_id), status=VALUES(status), amount_due=VALUES(amount_due), amount_paid=VALUES(amount_paid), assessed_amount=VALUES(assessed_amount)'
            );
            $statement->execute([(int) $row['id'], (int) $row['billId'], (int) $row['studentId'], statusForDb($row['status'] ?? ''),
                (string) ($row['amountDue'] ?? 0), (string) ($row['amountPaid'] ?? 0), (string) ($row['assessedAmount'] ?? 0)]);
        }

        foreach ($transactionUpserts as $row) {
            $statement = $pdo->prepare(
                'INSERT INTO transaksi (id, class_id, type, payment_method, amount, note, student_id, bill_student_id, created_by, created_at, affects_kas, is_refund, is_pending_refund)
                 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                 ON DUPLICATE KEY UPDATE class_id=VALUES(class_id), type=VALUES(type), payment_method=VALUES(payment_method), amount=VALUES(amount), note=VALUES(note), student_id=VALUES(student_id), bill_student_id=VALUES(bill_student_id), created_by=VALUES(created_by), created_at=VALUES(created_at), affects_kas=VALUES(affects_kas), is_refund=VALUES(is_refund), is_pending_refund=VALUES(is_pending_refund)'
            );
            $statement->execute([(int) $row['id'], (int) $row['classId'], enumValue($row['type'] ?? '') === 'masuk' ? 'MASUK' : 'KELUAR',
                methodForDb($row['method'] ?? ''), (string) ($row['amount'] ?? 0), (string) ($row['description'] ?? ''),
                $row['studentId'] ?? null, $row['billStudentId'] ?? null, (string) ($row['createdBy'] ?? ''),
                sqlDateTime($row['createdAt'] ?? null) ?? date('Y-m-d H:i:s'), !empty($row['affectsKas']) ? 1 : 0,
                !empty($row['isRefund']) ? 1 : 0, !empty($row['isPendingRefund']) ? 1 : 0]);
        }

        foreach ($notificationUpserts as $row) {
            $statement = $pdo->prepare(
                'INSERT INTO notifications (id, student_id, bill_id, title, message, is_read, created_at)
                 VALUES (?, ?, ?, ?, ?, ?, ?)
                 ON DUPLICATE KEY UPDATE student_id=VALUES(student_id), bill_id=VALUES(bill_id), title=VALUES(title), message=VALUES(message), is_read=VALUES(is_read), created_at=VALUES(created_at)'
            );
            $statement->execute([(int) $row['id'], (int) $row['studentId'], $row['billId'] ?? null,
                (string) ($row['title'] ?? ''), (string) ($row['message'] ?? ''), !empty($row['isRead']) ? 1 : 0,
                sqlDateTime($row['createdAt'] ?? null) ?? date('Y-m-d H:i:s')]);
        }
    } elseif ($transactionUpserts) {
        // Wali Kelas can rename a student (transaction descriptions follow that name) or
        // remove the student's links, but cannot alter amount, method, or cash accounting.
        $updateDescription = $pdo->prepare('UPDATE transaksi SET note = ? WHERE id = ? AND class_id = ? AND student_id = ?');
        $unlinkStudent = $pdo->prepare('UPDATE transaksi SET student_id = NULL, bill_student_id = NULL WHERE id = ? AND class_id = ?');
        foreach ($transactionUpserts as $row) {
            if (($row['studentId'] ?? null) !== null) {
                $updateDescription->execute([(string) ($row['description'] ?? ''), (int) $row['id'], $classId, (int) $row['studentId']]);
            } elseif (($row['billStudentId'] ?? null) === null) {
                $unlinkStudent->execute([(int) $row['id'], $classId]);
            }
        }
    }

    $deleteIds = static function (array $values): array {
        return array_values(array_unique(array_filter(array_map('intval', $values), static fn(int $id): bool => $id > 0)));
    };
    $deleteOne = static function (string $sql, int $id, bool $isTreasurer, ?int $classId) use ($pdo): void {
        $statement = $pdo->prepare($sql);
        $statement->execute($isTreasurer ? [$id] : [$id, $classId]);
    };

    if ($isTreasurer) {
        foreach ($deleteIds(rowsFor($input, 'notifications', 'deletes')) as $id)
            $deleteOne('DELETE FROM notifications WHERE id = ?', $id, true, $classId);
        foreach ($deleteIds(rowsFor($input, 'transactions', 'deletes')) as $id)
            $deleteOne('DELETE FROM transaksi WHERE id = ?', $id, true, $classId);
        foreach ($deleteIds(rowsFor($input, 'billStudents', 'deletes')) as $id)
            $deleteOne('DELETE FROM bill_students WHERE id = ?', $id, true, $classId);
        foreach ($deleteIds(rowsFor($input, 'bills', 'deletes')) as $id)
            $deleteOne('DELETE FROM bills WHERE id = ?', $id, true, $classId);
        foreach ($deleteIds(rowsFor($input, 'students', 'deletes')) as $id)
            $deleteOne('DELETE FROM students WHERE id = ?', $id, true, $classId);
        foreach ($deleteIds(rowsFor($input, 'schedules', 'deletes')) as $id)
            $deleteOne('DELETE FROM weekly_billing_schedules WHERE id = ?', $id, true, $classId);
        foreach ($deleteIds(rowsFor($input, 'staff', 'deletes')) as $id)
            $deleteOne('DELETE FROM users WHERE id = ?', $id, true, $classId);
        foreach ($deleteIds(rowsFor($input, 'classes', 'deletes')) as $id)
            $deleteOne('DELETE FROM classes WHERE id = ?', $id, true, $classId);
    } else {
        foreach ($deleteIds(rowsFor($input, 'notifications', 'deletes')) as $id)
            $deleteOne('DELETE n FROM notifications n JOIN students s ON s.id = n.student_id WHERE n.id = ? AND s.class_id = ?', $id, false, $classId);
        foreach ($deleteIds(rowsFor($input, 'billStudents', 'deletes')) as $id)
            $deleteOne('DELETE bs FROM bill_students bs JOIN bills b ON b.id = bs.bill_id WHERE bs.id = ? AND b.class_id = ?', $id, false, $classId);
        foreach ($deleteIds(rowsFor($input, 'students', 'deletes')) as $id)
            $deleteOne('DELETE FROM students WHERE id = ? AND class_id = ?', $id, false, $classId);
    }

    $pdo->commit();
    sendResponse(200, ['message' => 'Changes saved.']);
} catch (Throwable $exception) {
    if (isset($pdo) && $pdo instanceof PDO && $pdo->inTransaction()) $pdo->rollBack();
    error_log('KasMate API sync failed: ' . $exception->getMessage());
    sendResponse(500, null, 'The changes could not be saved. Please refresh and try again.');
}
