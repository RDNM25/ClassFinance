-- Add the method used for each cash transaction.
-- Existing rows default to cash until their original payment method can be confirmed.

ALTER TABLE `transaksi`
  ADD COLUMN `payment_method` varchar(16) NOT NULL DEFAULT 'TUNAI' AFTER `type`;
