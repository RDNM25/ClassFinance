<?php
require '../config/database.php';
require '../helpers/response.php';
require '../middleware/auth.php';

$pdo = getDb();
requireAuth($pdo);

$stmt = $pdo->query('SELECT id, nama, owed FROM students ORDER BY nama ASC');
$students = $stmt->fetchAll();

sendResponse(200, $students);