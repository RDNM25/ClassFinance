-- Retire old one-off bills without deleting their payment or notification history.
-- New bill rows must be explicitly created from a weekly schedule by the app;
-- rows without a complete weekly period are archived by default.

ALTER TABLE `bills`
  ADD COLUMN `is_archived` tinyint(1) NOT NULL DEFAULT 1 AFTER `period_end`;

UPDATE `bills`
SET `is_archived` = CASE
  WHEN `weekly_schedule_id` IS NOT NULL
    AND `period_start` IS NOT NULL
    AND `period_end` IS NOT NULL THEN 0
  ELSE 1
END;

ALTER TABLE `bills`
  ADD KEY `bills_active_period_idx` (`class_id`, `is_archived`, `period_start`);
