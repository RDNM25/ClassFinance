-- Bearer sessions used by KasMateAPI for the desktop and future mobile clients.
-- No foreign key is added here so imported users tables with different signedness
-- or storage engines can use this migration safely.
CREATE TABLE IF NOT EXISTS `tokens` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `user_id` int(11) NOT NULL,
  `token` char(64) NOT NULL,
  `expires_at` datetime NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`id`),
  UNIQUE KEY `tokens_token_uq` (`token`),
  KEY `tokens_user_expiry_idx` (`user_id`, `expires_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
