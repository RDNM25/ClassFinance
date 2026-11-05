-- Keep the original amount assessed for each student and bill/week separately
-- from the remaining balance. For rows created before this migration, the
-- original charge is the remaining balance plus the amount already paid.

ALTER TABLE `bill_students`
  ADD COLUMN `assessed_amount` bigint(20) NULL AFTER `amount_paid`;

UPDATE `bill_students`
SET `assessed_amount` = `amount_due` + `amount_paid`
WHERE `assessed_amount` IS NULL;

ALTER TABLE `bill_students`
  MODIFY COLUMN `assessed_amount` bigint(20) NOT NULL;

ALTER TABLE `transaksi`
  ADD KEY `transaksi_bill_ledger_idx` (`bill_student_id`, `type`, `is_refund`, `is_pending_refund`);

-- Individual payment transactions are the source of truth when present.
-- Cached totals remain as a fallback for imported/legacy rows without linked
-- transaction history. The view exposes a reconstructed balance per student
-- per bill, including weekly period dates.
CREATE OR REPLACE VIEW `bill_student_balances` AS
SELECT
  balance.`id`,
  balance.`bill_id`,
  balance.`student_id`,
  balance.`weekly_schedule_id`,
  balance.`period_start`,
  balance.`period_end`,
  balance.`assessed_amount`,
  balance.`amount_paid`,
  balance.`amount_due`,
  CASE
    WHEN balance.`pending_refund_count` > 0 THEN 'PROSES'
    WHEN balance.`amount_due` <= 0 THEN 'LUNAS'
    WHEN balance.`amount_paid` > 0 THEN 'SEBAGIAN'
    ELSE 'BELUM_BAYAR'
  END AS `status`
FROM (
  SELECT
    bs.`id`,
    bs.`bill_id`,
    bs.`student_id`,
    b.`weekly_schedule_id`,
    b.`period_start`,
    b.`period_end`,
    bs.`assessed_amount`,
    CASE
      WHEN COALESCE(payments.`payment_count`, 0) > 0 THEN payments.`paid_total`
      ELSE bs.`amount_paid`
    END AS `amount_paid`,
    CASE
      WHEN COALESCE(payments.`payment_count`, 0) > 0
        THEN GREATEST(0, bs.`assessed_amount` - payments.`paid_total`)
      ELSE bs.`amount_due`
    END AS `amount_due`,
    COALESCE(refunds.`pending_refund_count`, 0) AS `pending_refund_count`
  FROM `bill_students` bs
  INNER JOIN `bills` b ON b.`id` = bs.`bill_id`
  LEFT JOIN (
    SELECT `bill_student_id`, COUNT(*) AS `payment_count`, SUM(`amount`) AS `paid_total`
    FROM `transaksi`
    WHERE `bill_student_id` IS NOT NULL
      AND `type` = 'MASUK'
      AND `is_refund` = 0
    GROUP BY `bill_student_id`
  ) payments ON payments.`bill_student_id` = bs.`id`
  LEFT JOIN (
    SELECT `bill_student_id`, COUNT(*) AS `pending_refund_count`
    FROM `transaksi`
    WHERE `bill_student_id` IS NOT NULL AND `is_pending_refund` = 1
    GROUP BY `bill_student_id`
  ) refunds ON refunds.`bill_student_id` = bs.`id`
) balance;
