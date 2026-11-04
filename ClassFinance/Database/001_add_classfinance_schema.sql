-- Incremental migration for the existing `kasmate` MariaDB database.
-- Run this from phpMyAdmin with kasmate selected. Existing records are preserved.

ALTER TABLE `users`
  ADD COLUMN `name` varchar(255) NULL AFTER `id`,
  ADD COLUMN `role` varchar(30) NOT NULL DEFAULT 'bendahara' AFTER `password_hash`;

UPDATE `users` SET `name` = `username` WHERE `name` IS NULL OR `name` = '';

ALTER TABLE `users`
  MODIFY COLUMN `name` varchar(255) NOT NULL;

CREATE TABLE `classes` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(150) NOT NULL,
  `academic_year` varchar(20) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

ALTER TABLE `students`
  ADD COLUMN `nis` varchar(50) NULL AFTER `nama`,
  ADD COLUMN `class_id` int(11) NULL AFTER `nis`,
  ADD COLUMN `saldo_titipan` bigint(20) NOT NULL DEFAULT 0 AFTER `owed`,
  ADD KEY `students_class_id_idx` (`class_id`),
  ADD CONSTRAINT `students_class_id_fk` FOREIGN KEY (`class_id`) REFERENCES `classes` (`id`) ON DELETE SET NULL;

CREATE TABLE `bills` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `class_id` int(11) NOT NULL,
  `name` varchar(255) NOT NULL,
  `amount` bigint(20) NOT NULL,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  `updated_at` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`),
  KEY `bills_class_id_idx` (`class_id`),
  CONSTRAINT `bills_class_id_fk` FOREIGN KEY (`class_id`) REFERENCES `classes` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE `bill_students` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `bill_id` int(11) NOT NULL,
  `student_id` int(11) NOT NULL,
  `status` enum('BELUM_BAYAR','SEBAGIAN','PROSES','LUNAS') NOT NULL DEFAULT 'BELUM_BAYAR',
  `amount_due` bigint(20) NOT NULL,
  `amount_paid` bigint(20) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `bill_students_bill_student_uq` (`bill_id`,`student_id`),
  KEY `bill_students_student_id_idx` (`student_id`),
  CONSTRAINT `bill_students_bill_id_fk` FOREIGN KEY (`bill_id`) REFERENCES `bills` (`id`) ON DELETE CASCADE,
  CONSTRAINT `bill_students_student_id_fk` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

ALTER TABLE `transaksi`
  ADD COLUMN `class_id` int(11) NULL AFTER `id`,
  ADD COLUMN `bill_student_id` int(11) NULL AFTER `student_id`,
  ADD COLUMN `created_by` varchar(255) NULL AFTER `bill_student_id`,
  ADD COLUMN `affects_kas` tinyint(1) NOT NULL DEFAULT 1 AFTER `created_at`,
  ADD COLUMN `is_refund` tinyint(1) NOT NULL DEFAULT 0 AFTER `affects_kas`,
  ADD COLUMN `is_pending_refund` tinyint(1) NOT NULL DEFAULT 0 AFTER `is_refund`,
  ADD KEY `transaksi_class_id_idx` (`class_id`),
  ADD KEY `transaksi_bill_student_id_idx` (`bill_student_id`),
  ADD CONSTRAINT `transaksi_class_id_fk` FOREIGN KEY (`class_id`) REFERENCES `classes` (`id`) ON DELETE SET NULL,
  ADD CONSTRAINT `transaksi_bill_student_id_fk` FOREIGN KEY (`bill_student_id`) REFERENCES `bill_students` (`id`) ON DELETE SET NULL;

CREATE TABLE `notifications` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `student_id` int(11) NOT NULL,
  `bill_id` int(11) NULL,
  `title` varchar(255) NOT NULL,
  `message` text NOT NULL,
  `is_read` tinyint(1) NOT NULL DEFAULT 0,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`id`),
  KEY `notifications_student_created_idx` (`student_id`,`created_at`),
  KEY `notifications_bill_id_idx` (`bill_id`),
  CONSTRAINT `notifications_student_id_fk` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE,
  CONSTRAINT `notifications_bill_id_fk` FOREIGN KEY (`bill_id`) REFERENCES `bills` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- `payments` is retained to preserve existing data. New bill payments should be
-- recorded in `transaksi` and linked to `bill_students` to avoid duplicate records.
-- Existing rows remain unassigned until their class/bill mapping is known.
