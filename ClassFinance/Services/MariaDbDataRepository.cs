using System;
using System.Globalization;
using System.Linq;
using ClassFinance.Models;
using MySqlConnector;

namespace ClassFinance.Services
{
    internal sealed class MariaDbDataRepository
    {
        private readonly string _connectionString;

        public MariaDbDataRepository()
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = Environment.GetEnvironmentVariable("KASMATE_DB_HOST") ?? "127.0.0.1",
                Port = uint.TryParse(Environment.GetEnvironmentVariable("KASMATE_DB_PORT"), out var port) ? port : 3306,
                Database = Environment.GetEnvironmentVariable("KASMATE_DB_NAME") ?? "kasmate",
                UserID = Environment.GetEnvironmentVariable("KASMATE_DB_USER") ?? "root",
                Password = Environment.GetEnvironmentVariable("KASMATE_DB_PASSWORD") ?? string.Empty,
                ConnectionTimeout = 5,
                AllowUserVariables = true
            };
            _connectionString = builder.ConnectionString;
        }

        public void Load(DataStore store)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using (var command = new MySqlCommand("SELECT id, name, academic_year FROM classes ORDER BY id", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read())
                    store.KelasList.Add(new Kelas { Id = reader.GetInt32("id"), Name = reader.GetString("name"), AcademicYear = reader.GetString("academic_year") });

            if (store.KelasList.Count == 0)
            {
                using var command = new MySqlCommand("INSERT INTO classes (name, academic_year) VALUES ('XI PPLG 2', '2025/2026')", connection);
                command.ExecuteNonQuery();
                store.KelasList.Add(new Kelas { Id = (int)command.LastInsertedId, Name = "XI PPLG 2", AcademicYear = "2025/2026" });
            }
            var defaultClassId = store.KelasList[0].Id;

            using (var command = new MySqlCommand("SELECT id, name, username, password_hash, role, class_id FROM users ORDER BY id", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var role = reader.GetString("role");
                    User user = role.Equals("wali_kelas", StringComparison.OrdinalIgnoreCase) || role.Equals("walikelas", StringComparison.OrdinalIgnoreCase)
                        ? new WaliKelas()
                        : new Bendahara();
                    user.Id = reader.GetInt32("id");
                    user.Name = reader.GetString("name");
                    user.Username = reader.GetString("username");
                    user.PasswordHash = reader.GetString("password_hash");
                    if (user is WaliKelas wali) wali.KelasId = reader.IsDBNull(reader.GetOrdinal("class_id")) ? defaultClassId : reader.GetInt32("class_id");
                    store.Users.Add(user);
                }
            }

            using (var command = new MySqlCommand("SELECT id, nama, nis, class_id, saldo_titipan FROM students ORDER BY id", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    store.Users.Add(new Siswa
                    {
                        Id = reader.GetInt32("id"), Name = reader.GetString("nama"),
                        Nis = reader.IsDBNull(reader.GetOrdinal("nis")) ? string.Empty : reader.GetString("nis"),
                        KelasId = reader.IsDBNull(reader.GetOrdinal("class_id")) ? defaultClassId : reader.GetInt32("class_id"),
                        SaldoTitipan = ReadDecimal(reader, "saldo_titipan")
                    });
                }
            }

            using (var command = new MySqlCommand("SELECT id, class_id, name, amount, created_at FROM bills ORDER BY id", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read())
                    store.TagihanList.Add(new Tagihan { Id = reader.GetInt32("id"), KelasId = reader.GetInt32("class_id"), Name = reader.GetString("name"), Amount = ReadDecimal(reader, "amount"), CreatedDate = reader.GetDateTime("created_at") });

            using (var command = new MySqlCommand("SELECT id, bill_id, student_id, status, amount_due, amount_paid FROM bill_students ORDER BY id", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read())
                    store.TagihanSiswaList.Add(new TagihanSiswa { Id = reader.GetInt32("id"), TagihanId = reader.GetInt32("bill_id"), SiswaId = reader.GetInt32("student_id"), Status = ParseStatus(reader.GetString("status")), AmountDue = ReadDecimal(reader, "amount_due"), JumlahDibayar = ReadDecimal(reader, "amount_paid") });

            using (var command = new MySqlCommand("SELECT id, class_id, type, amount, note, student_id, bill_student_id, created_by, created_at, affects_kas, is_refund, is_pending_refund FROM transaksi ORDER BY id", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read())
                    store.TransaksiList.Add(new Transaksi
                    {
                        Id = reader.GetInt32("id"), KelasId = reader.IsDBNull(reader.GetOrdinal("class_id")) ? defaultClassId : reader.GetInt32("class_id"),
                        Type = reader.GetString("type").Equals("MASUK", StringComparison.OrdinalIgnoreCase) ? JenisTransaksi.Masuk : JenisTransaksi.Keluar,
                        Amount = ReadDecimal(reader, "amount"), Description = reader.IsDBNull(reader.GetOrdinal("note")) ? string.Empty : reader.GetString("note"),
                        SiswaId = reader.IsDBNull(reader.GetOrdinal("student_id")) ? null : reader.GetInt32("student_id"),
                        TagihanSiswaId = reader.IsDBNull(reader.GetOrdinal("bill_student_id")) ? null : reader.GetInt32("bill_student_id"),
                        CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? string.Empty : reader.GetString("created_by"), Date = reader.GetDateTime("created_at"),
                        AffectsKas = reader.GetBoolean("affects_kas"), IsRefund = reader.GetBoolean("is_refund"), IsPendingRefund = reader.GetBoolean("is_pending_refund")
                    });

            using (var command = new MySqlCommand("SELECT id, student_id, bill_id, title, message, is_read, created_at FROM notifications ORDER BY id", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read())
                    store.NotifikasiList.Add(new Notifikasi { Id = reader.GetInt32("id"), UserId = reader.GetInt32("student_id"), BillId = reader.IsDBNull(reader.GetOrdinal("bill_id")) ? null : reader.GetInt32("bill_id"), Title = reader.GetString("title"), Message = reader.GetString("message"), IsRead = reader.GetBoolean("is_read"), CreatedAt = reader.GetDateTime("created_at") });

            // The imported starter data has no class assignment yet. Associate it with the
            // first class so existing students and transactions appear in the desktop app.
            using (var command = new MySqlCommand("UPDATE students SET class_id = @classId WHERE class_id IS NULL", connection))
            {
                command.Parameters.AddWithValue("@classId", defaultClassId);
                command.ExecuteNonQuery();
            }
            using (var command = new MySqlCommand("UPDATE transaksi SET class_id = @classId WHERE class_id IS NULL", connection))
            {
                command.Parameters.AddWithValue("@classId", defaultClassId);
                command.ExecuteNonQuery();
            }
            store.SetNextIds();
        }

        public void Save(DataStore store)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var item in store.KelasList)
                    Execute(connection, transaction, "INSERT INTO classes (id,name,academic_year) VALUES (@id,@name,@year) ON DUPLICATE KEY UPDATE name=VALUES(name), academic_year=VALUES(academic_year)", ("@id", item.Id), ("@name", item.Name), ("@year", item.AcademicYear));

                foreach (var item in store.Users.Where(x => x is not Siswa))
                {
                    var role = item is WaliKelas ? "wali_kelas" : "bendahara";
                    Execute(connection, transaction, "INSERT INTO users (id,name,username,password_hash,role,class_id) VALUES (@id,@name,@username,@hash,@role,@classId) ON DUPLICATE KEY UPDATE name=VALUES(name), username=VALUES(username), password_hash=VALUES(password_hash), role=VALUES(role), class_id=VALUES(class_id)", ("@id", item.Id), ("@name", item.Name), ("@username", item.Username), ("@hash", item.PasswordHash), ("@role", role), ("@classId", item is WaliKelas wali ? wali.KelasId : null));
                }

                foreach (var item in store.Users.OfType<Siswa>())
                    Execute(connection, transaction, "INSERT INTO students (id,nama,nis,class_id,saldo_titipan) VALUES (@id,@name,@nis,@classId,@balance) ON DUPLICATE KEY UPDATE nama=VALUES(nama), nis=VALUES(nis), class_id=VALUES(class_id), saldo_titipan=VALUES(saldo_titipan)", ("@id", item.Id), ("@name", item.Name), ("@nis", NullIfEmpty(item.Nis)), ("@classId", item.KelasId), ("@balance", item.SaldoTitipan));

                foreach (var item in store.TagihanList)
                    Execute(connection, transaction, "INSERT INTO bills (id,class_id,name,amount,created_at) VALUES (@id,@classId,@name,@amount,@createdAt) ON DUPLICATE KEY UPDATE class_id=VALUES(class_id), name=VALUES(name), amount=VALUES(amount), created_at=VALUES(created_at)", ("@id", item.Id), ("@classId", item.KelasId), ("@name", item.Name), ("@amount", item.Amount), ("@createdAt", item.CreatedDate));

                foreach (var item in store.TagihanSiswaList)
                    Execute(connection, transaction, "INSERT INTO bill_students (id,bill_id,student_id,status,amount_due,amount_paid) VALUES (@id,@billId,@studentId,@status,@due,@paid) ON DUPLICATE KEY UPDATE bill_id=VALUES(bill_id), student_id=VALUES(student_id), status=VALUES(status), amount_due=VALUES(amount_due), amount_paid=VALUES(amount_paid)", ("@id", item.Id), ("@billId", item.TagihanId), ("@studentId", item.SiswaId), ("@status", DbStatus(item.Status)), ("@due", item.AmountDue), ("@paid", item.JumlahDibayar));

                foreach (var item in store.TransaksiList)
                    Execute(connection, transaction, "INSERT INTO transaksi (id,class_id,type,amount,note,student_id,bill_student_id,created_by,created_at,affects_kas,is_refund,is_pending_refund) VALUES (@id,@classId,@type,@amount,@note,@studentId,@billStudentId,@createdBy,@createdAt,@affectsKas,@isRefund,@pendingRefund) ON DUPLICATE KEY UPDATE class_id=VALUES(class_id), type=VALUES(type), amount=VALUES(amount), note=VALUES(note), student_id=VALUES(student_id), bill_student_id=VALUES(bill_student_id), created_by=VALUES(created_by), created_at=VALUES(created_at), affects_kas=VALUES(affects_kas), is_refund=VALUES(is_refund), is_pending_refund=VALUES(is_pending_refund)",
                        ("@id", item.Id), ("@classId", item.KelasId), ("@type", item.Type == JenisTransaksi.Masuk ? "MASUK" : "KELUAR"), ("@amount", item.Amount), ("@note", item.Description), ("@studentId", item.SiswaId), ("@billStudentId", item.TagihanSiswaId), ("@createdBy", item.CreatedBy), ("@createdAt", item.Date), ("@affectsKas", item.AffectsKas), ("@isRefund", item.IsRefund), ("@pendingRefund", item.IsPendingRefund));

                foreach (var item in store.NotifikasiList)
                    Execute(connection, transaction, "INSERT INTO notifications (id,student_id,bill_id,title,message,is_read,created_at) VALUES (@id,@studentId,@billId,@title,@message,@isRead,@createdAt) ON DUPLICATE KEY UPDATE student_id=VALUES(student_id), bill_id=VALUES(bill_id), title=VALUES(title), message=VALUES(message), is_read=VALUES(is_read), created_at=VALUES(created_at)", ("@id", item.Id), ("@studentId", item.UserId), ("@billId", item.BillId), ("@title", item.Title), ("@message", item.Message), ("@isRead", item.IsRead), ("@createdAt", item.CreatedAt));

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void DeleteStudent(int id) => Execute("DELETE FROM students WHERE id=@id", ("@id", id));
        public void DeleteTransaction(int id) => Execute("DELETE FROM transaksi WHERE id=@id", ("@id", id));
        public void DeleteBill(int id) => Execute("DELETE FROM bills WHERE id=@id", ("@id", id));

        private void Execute(string sql, params (string Name, object Value)[] values)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            Execute(connection, null, sql, values);
        }

        private static void Execute(MySqlConnection connection, MySqlTransaction transaction, string sql, params (string Name, object Value)[] values)
        {
            using var command = new MySqlCommand(sql, connection, transaction);
            foreach (var (name, value) in values)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        private static object NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

        private static decimal ReadDecimal(MySqlDataReader reader, string column) =>
            Convert.ToDecimal(reader[column], CultureInfo.InvariantCulture);

        private static StatusTagihan ParseStatus(string status) => status.ToUpperInvariant() switch
        {
            "SEBAGIAN" => StatusTagihan.Sebagian,
            "PROSES" => StatusTagihan.Proses,
            "LUNAS" => StatusTagihan.Lunas,
            _ => StatusTagihan.BelumBayar
        };

        private static string DbStatus(StatusTagihan status) => status switch
        {
            StatusTagihan.Sebagian => "SEBAGIAN",
            StatusTagihan.Proses => "PROSES",
            StatusTagihan.Lunas => "LUNAS",
            _ => "BELUM_BAYAR"
        };
    }
}
