<?php
header('Content-Type: application/json; charset=utf-8');
$allowedOrigins = array_filter(array_map('trim', explode(',', getenv('KASMATE_CORS_ORIGINS') ?: '')));
$requestOrigin = $_SERVER['HTTP_ORIGIN'] ?? '';
if ($requestOrigin !== '' && in_array($requestOrigin, $allowedOrigins, true)) {
    header('Access-Control-Allow-Origin: ' . $requestOrigin);
    header('Vary: Origin');
}
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, Authorization');

if (($_SERVER['REQUEST_METHOD'] ?? '') === 'OPTIONS') {
    http_response_code(204);
    exit;
}

function sendResponse(int $code, $data = null, ?string $error = null): void
{
    http_response_code($code);
    echo json_encode([
        'success' => $error === null,
        'data' => $data,
        'error' => $error,
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_INVALID_UTF8_SUBSTITUTE);
    exit;
}

function readJsonInput(): array
{
    $raw = file_get_contents('php://input');
    $input = json_decode($raw ?: '', true);
    if (!is_array($input)) {
        sendResponse(400, null, 'Request body must be valid JSON.');
    }
    return $input;
}

function isoDateTime(?string $value): ?string
{
    if ($value === null || $value === '') return null;
    $timestamp = strtotime($value);
    return $timestamp === false ? null : date('Y-m-d\\TH:i:s', $timestamp);
}
