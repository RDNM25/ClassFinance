-- Add the class association used by WaliKelas accounts in the desktop app.
ALTER TABLE `users`
  ADD COLUMN `class_id` int(11) NULL AFTER `role`,
  ADD KEY `users_class_id_idx` (`class_id`),
  ADD CONSTRAINT `users_class_id_fk` FOREIGN KEY (`class_id`) REFERENCES `classes` (`id`) ON DELETE SET NULL;
