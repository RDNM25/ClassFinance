-- Weekly billing support. Run once with the `kasmate` database selected in phpMyAdmin.
-- Existing bills remain unchanged; only bills generated from a weekly schedule have a period.

CREATE TABLE `weekly_billing_schedules` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `class_id` int(11) NOT NULL,
  `name` varchar(255) NOT NULL,
  `amount` bigint(20) NOT NULL,
  `starts_on` date NOT NULL,
  `ends_on` date NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`id`),
  UNIQUE KEY `weekly_billing_schedules_class_uq` (`class_id`),
  CONSTRAINT `weekly_billing_schedules_class_fk` FOREIGN KEY (`class_id`) REFERENCES `classes` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

ALTER TABLE `bills`
  ADD COLUMN `weekly_schedule_id` int(11) NULL AFTER `amount`,
  ADD COLUMN `period_start` date NULL AFTER `weekly_schedule_id`,
  ADD COLUMN `period_end` date NULL AFTER `period_start`,
  ADD UNIQUE KEY `bills_schedule_period_uq` (`weekly_schedule_id`, `period_start`),
  ADD KEY `bills_period_start_idx` (`period_start`),
  ADD CONSTRAINT `bills_weekly_schedule_fk` FOREIGN KEY (`weekly_schedule_id`) REFERENCES `weekly_billing_schedules` (`id`) ON DELETE SET NULL;
