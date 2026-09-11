using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ClassFinance.Models;

namespace ClassFinance.Services
{
    /// <summary>
    /// In-memory "database" for the app (acts as the Model layer's data access point).
    /// Replace with EF Core / SQLite later without touching the Views.
    /// </summary>
    public sealed class DataStore
    {
        private static readonly Lazy<DataStore> _instance = new(() => new DataStore());
        public static DataStore Instance => _instance.Value;

        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<Kelas> KelasList { get; } = new();
        public ObservableCollection<Tagihan> TagihanList { get; } = new();
        public ObservableCollection<TagihanSiswa> TagihanSiswaList { get; } = new();
        public ObservableCollection<Transaksi> TransaksiList { get; } = new();
        public ObservableCollection<Notifikasi> NotifikasiList { get; } = new();

        private int _userId = 1;
        private int _kelasId = 1;
        private int _tagihanId = 1;
        private int _tagihanSiswaId = 1;
        private int _transaksiId = 1;
        private int _notifId = 1;

        public int NextUserId() => _userId++;
        public int NextKelasId() => _kelasId++;
        public int NextTagihanId() => _tagihanId++;
        public int NextTagihanSiswaId() => _tagihanSiswaId++;
        public int NextTransaksiId() => _transaksiId++;
        public int NextNotifikasiId() => _notifId++;

        private DataStore()
        {
            Seed();
        }

        public Transaksi TambahTransaksi(int kelasId, JenisTransaksi type, decimal amount, DateTime date,
            string description, int? tagihanSiswaId, string createdBy, string proofFile = null, int? siswaId = null)
        {
            var t = new Transaksi
            {
                Id = NextTransaksiId(),
                KelasId = kelasId,
                Type = type,
                Amount = amount,
                Date = date,
                Description = description,
                TagihanSiswaId = tagihanSiswaId,
                CreatedBy = createdBy,
                ProofFile = proofFile,
                SiswaId = siswaId
            };
            TransaksiList.Add(t);
            return t;
        }

        public Tagihan BuatTagihan(int kelasId, string name, decimal amount)
        {
            var tagihan = new Tagihan
            {
                Id = NextTagihanId(),
                KelasId = kelasId,
                Name = name,
                Amount = amount,
                CreatedDate = DateTime.Now
            };
            TagihanList.Add(tagihan);
            tagihan.GenerateTagihanSiswa(this);
            return tagihan;
        }

        public void HapusSiswa(int siswaId)
        {
            var siswa = Users.OfType<Siswa>().FirstOrDefault(s => s.Id == siswaId);
            if (siswa != null) Users.Remove(siswa);
        }

        private void Seed()
        {
            // Only the class itself and the login accounts are seeded.
            // Everything else (students, transactions, tagihan) starts empty
            // so the app opens with Uang Kas = Rp 0 and no history — the user
            // adds all of that themselves via Tambah Siswa / Tambah Transaksi / Buat Tagihan.
            var kelas = new Kelas { Id = NextKelasId(), Name = "XI PPLG 2", AcademicYear = "2025/2026" };
            KelasList.Add(kelas);

            var bendahara = new Bendahara { Id = NextUserId(), Name = "Admin Bendahara", Username = "admin", PasswordHash = "admin" };
            var wali = new WaliKelas { Id = NextUserId(), Name = "Bu Sari", Username = "wali", PasswordHash = "wali123", KelasId = kelas.Id };

            Users.Add(bendahara);
            Users.Add(wali);
        }
    }
}
