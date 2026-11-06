using System;
using System.Linq;
using System.Threading.Tasks;
using ClassFinance.Models;

namespace ClassFinance.Services
{
    internal sealed class ApiDataRepository
    {
        public async Task<ApiIdCounters> LoadAsync(DataStore store)
        {
            var snapshot = await KasMateApiClient.Instance.GetSnapshotAsync();

            store.KelasList.Clear();
            foreach (var row in snapshot.Classes)
                store.KelasList.Add(new Kelas { Id = row.Id, Name = row.Name, AcademicYear = row.AcademicYear });

            store.JadwalTagihanMingguanList.Clear();
            foreach (var row in snapshot.Schedules)
                store.JadwalTagihanMingguanList.Add(new JadwalTagihanMingguan
                {
                    Id = row.Id, KelasId = row.ClassId, Name = row.Name, Amount = row.Amount,
                    StartsOn = row.StartsOn, EndsOn = row.EndsOn, IsActive = row.IsActive, CreatedDate = row.CreatedAt
                });

            store.Users.Clear();
            foreach (var row in snapshot.Staff)
            {
                User user = IsTeacherRole(row.Role) ? new WaliKelas() : new Bendahara();
                user.Id = row.Id;
                user.Name = row.Name;
                user.Username = row.Username;
                if (user is WaliKelas teacher)
                    teacher.KelasId = row.ClassId ?? store.KelasList.FirstOrDefault()?.Id ?? 0;
                store.Users.Add(user);
            }
            foreach (var row in snapshot.Students)
            {
                store.Users.Add(new Siswa
                {
                    Id = row.Id, Name = row.Name, Nis = row.Nis ?? string.Empty,
                    KelasId = row.ClassId, SaldoTitipan = row.Balance
                });
            }

            store.TagihanList.Clear();
            foreach (var row in snapshot.Bills)
                store.TagihanList.Add(new Tagihan
                {
                    Id = row.Id, KelasId = row.ClassId, Name = row.Name, Amount = row.Amount,
                    CreatedDate = row.CreatedAt, WeeklyScheduleId = row.ScheduleId,
                    PeriodStart = row.PeriodStart, PeriodEnd = row.PeriodEnd, IsArchived = row.IsArchived
                });

            store.TagihanSiswaList.Clear();
            foreach (var row in snapshot.BillStudents)
                store.TagihanSiswaList.Add(new TagihanSiswa
                {
                    Id = row.Id, TagihanId = row.BillId, SiswaId = row.StudentId,
                    Status = ParseStatus(row.Status), AmountDue = row.AmountDue,
                    JumlahDibayar = row.AmountPaid, AssessedAmount = row.AssessedAmount
                });

            store.TransaksiList.Clear();
            foreach (var row in snapshot.Transactions)
                store.TransaksiList.Add(new Transaksi
                {
                    Id = row.Id, KelasId = row.ClassId, Type = ParseType(row.Type), Metode = ParseMethod(row.Method),
                    Amount = row.Amount, Description = row.Description ?? string.Empty,
                    SiswaId = row.StudentId, TagihanSiswaId = row.BillStudentId, CreatedBy = row.CreatedBy ?? string.Empty,
                    Date = row.CreatedAt, AffectsKas = row.AffectsKas, IsRefund = row.IsRefund,
                    IsPendingRefund = row.IsPendingRefund
                });

            store.NotifikasiList.Clear();
            foreach (var row in snapshot.Notifications)
                store.NotifikasiList.Add(new Notifikasi
                {
                    Id = row.Id, UserId = row.StudentId, BillId = row.BillId,
                    Title = row.Title, Message = row.Message, IsRead = row.IsRead, CreatedAt = row.CreatedAt
                });

            return snapshot.IdCounters;
        }

        public Task ApplyAsync(DatabaseChanges changes) => KasMateApiClient.Instance.ApplyChangesAsync(changes);

        private static bool IsTeacherRole(string role) => string.Equals(
            role?.Replace("_", string.Empty), "walikelas", StringComparison.OrdinalIgnoreCase);

        private static StatusTagihan ParseStatus(string value) => value?.Trim().ToUpperInvariant() switch
        {
            "SEBAGIAN" => StatusTagihan.Sebagian,
            "PROSES" => StatusTagihan.Proses,
            "LUNAS" => StatusTagihan.Lunas,
            _ => StatusTagihan.BelumBayar
        };

        private static JenisTransaksi ParseType(string value) =>
            string.Equals(value, "Masuk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "MASUK", StringComparison.OrdinalIgnoreCase)
                ? JenisTransaksi.Masuk
                : JenisTransaksi.Keluar;

        private static MetodeTransaksi ParseMethod(string value) => value?.Trim().ToUpperInvariant() switch
        {
            "QRIS" => MetodeTransaksi.Qris,
            "TF" or "TRANSFER" => MetodeTransaksi.Tf,
            _ => MetodeTransaksi.Tunai
        };
    }
}
