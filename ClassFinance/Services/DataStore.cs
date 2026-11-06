using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClassFinance.Models;

namespace ClassFinance.Services
{
    /// <summary>Application data synchronized with the KasMate API.</summary>
    public sealed class DataStore : INotifyPropertyChanged
    {
        private static readonly Lazy<DataStore> _instance = new(() => new DataStore());
        public static DataStore Instance => _instance.Value;

        private readonly ApiDataRepository _repository = new();
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private bool _initialized;
        private DatabaseSnapshot _persistedSnapshot;

        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<Kelas> KelasList { get; } = new();
        public ObservableCollection<JadwalTagihanMingguan> JadwalTagihanMingguanList { get; } = new();
        public ObservableCollection<Tagihan> TagihanList { get; } = new();
        public ObservableCollection<TagihanSiswa> TagihanSiswaList { get; } = new();
        public ObservableCollection<Transaksi> TransaksiList { get; } = new();
        public ObservableCollection<Notifikasi> NotifikasiList { get; } = new();

        public int TransactionRevision { get; private set; }
        public int DataRevision { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler DataChanged;

        private int _userId = 1;
        private int _kelasId = 1;
        private int _weeklyScheduleId = 1;
        private int _tagihanId = 1;
        private int _tagihanSiswaId = 1;
        private int _transaksiId = 1;
        private int _notifId = 1;

        public int NextUserId() => _userId++;
        public int NextKelasId() => _kelasId++;
        public int NextWeeklyScheduleId() => _weeklyScheduleId++;
        public int NextTagihanId() => _tagihanId++;
        public int NextTagihanSiswaId() => _tagihanSiswaId++;
        public int NextTransaksiId() => _transaksiId++;
        public int NextNotifikasiId() => _notifId++;

        private DataStore()
        {
            TransaksiList.CollectionChanged += TransaksiList_CollectionChanged;
        }

        public async Task InitializeAsync()
        {
            // Called after API authentication, so data is fetched asynchronously without
            // blocking the login screen or exposing password hashes to the desktop app.
            var idCounters = await _repository.LoadAsync(this);

            SetNextIds(idCounters);
            _persistedSnapshot = DatabaseSnapshot.Capture(this);
            _initialized = true;
            DataRevision++;
        }

        public async Task SaveChangesAsync()
        {
            if (!_initialized) throw new InvalidOperationException("Database has not been initialized.");

            await _saveLock.WaitAsync();
            try
            {
                var currentSnapshot = DatabaseSnapshot.Capture(this);
                var changes = _persistedSnapshot.Diff(currentSnapshot);
                if (!changes.IsEmpty)
                {
                    await _repository.ApplyAsync(changes);
                    _persistedSnapshot = currentSnapshot;
                }
            }
            finally
            {
                _saveLock.Release();
            }

            DataRevision++;
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateStudentTransactionDescriptions(int studentId, string previousName, string currentName)
        {
            if (string.IsNullOrWhiteSpace(previousName) || string.IsNullOrWhiteSpace(currentName) ||
                string.Equals(previousName, currentName, StringComparison.Ordinal))
                return;

            var linkedBillStudentIds = TagihanSiswaList
                .Where(entry => entry.SiswaId == studentId)
                .Select(entry => entry.Id)
                .ToHashSet();

            foreach (var transaction in TransaksiList.Where(item =>
                         item.SiswaId == studentId ||
                         item.TagihanSiswaId.HasValue && linkedBillStudentIds.Contains(item.TagihanSiswaId.Value)))
            {
                if (!string.IsNullOrEmpty(transaction.Description) &&
                    transaction.Description.Contains(previousName, StringComparison.Ordinal))
                    transaction.Description = transaction.Description.Replace(previousName, currentName, StringComparison.Ordinal);
            }
        }

        internal void SetNextIds(ApiIdCounters counters)
        {
            _userId = Math.Max(counters.MaxUserId, Users.Select(u => u.Id).DefaultIfEmpty(0).Max()) + 1;
            _kelasId = Math.Max(counters.MaxClassId, KelasList.Select(x => x.Id).DefaultIfEmpty(0).Max()) + 1;
            _weeklyScheduleId = Math.Max(counters.MaxScheduleId, JadwalTagihanMingguanList.Select(x => x.Id).DefaultIfEmpty(0).Max()) + 1;
            _tagihanId = Math.Max(counters.MaxBillId, TagihanList.Select(x => x.Id).DefaultIfEmpty(0).Max()) + 1;
            _tagihanSiswaId = Math.Max(counters.MaxBillStudentId, TagihanSiswaList.Select(x => x.Id).DefaultIfEmpty(0).Max()) + 1;
            _transaksiId = Math.Max(counters.MaxTransactionId, TransaksiList.Select(x => x.Id).DefaultIfEmpty(0).Max()) + 1;
            _notifId = Math.Max(counters.MaxNotificationId, NotifikasiList.Select(x => x.Id).DefaultIfEmpty(0).Max()) + 1;
        }

        private void TransaksiList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            TransactionRevision++;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransactionRevision)));
        }

        public Transaksi CreateTransaction(int kelasId, JenisTransaksi type, decimal amount, DateTime date,
            string description, int? tagihanSiswaId, string createdBy, string proofFile = null, int? siswaId = null,
            bool affectsKas = true, MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            var t = new Transaksi
            {
                Id = NextTransaksiId(), KelasId = kelasId, Type = type, Amount = amount,
                Date = date, Description = description, TagihanSiswaId = tagihanSiswaId,
                CreatedBy = createdBy, ProofFile = proofFile, SiswaId = siswaId, AffectsKas = affectsKas,
                Metode = metode
            };
            TransaksiList.Add(t);
            return t;
        }

        public async Task<Transaksi> TambahTransaksiAsync(int kelasId, JenisTransaksi type, decimal amount, DateTime date,
            string description, int? tagihanSiswaId, string createdBy, string proofFile = null, int? siswaId = null,
            bool affectsKas = true, MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            var transaction = CreateTransaction(kelasId, type, amount, date, description, tagihanSiswaId,
                createdBy, proofFile, siswaId, affectsKas, metode);
            await SaveChangesAsync();
            return transaction;
        }

        public async Task HapusSiswaAsync(int siswaId)
        {
            var siswa = Users.OfType<Siswa>().FirstOrDefault(s => s.Id == siswaId);
            if (siswa == null) return;
            var entries = TagihanSiswaList.Where(x => x.SiswaId == siswaId).ToList();
            var entryIds = entries.Select(x => x.Id).ToHashSet();
            foreach (var transaction in TransaksiList.Where(x => x.SiswaId == siswaId || (x.TagihanSiswaId.HasValue && entryIds.Contains(x.TagihanSiswaId.Value))))
            {
                transaction.SiswaId = null;
                transaction.TagihanSiswaId = null;
            }
            Users.Remove(siswa);
            entries.ForEach(x => TagihanSiswaList.Remove(x));
            NotifikasiList.Where(x => x.UserId == siswaId).ToList().ForEach(x => NotifikasiList.Remove(x));
            await SaveChangesAsync();
        }

        public void RemoveTransaction(int transactionId)
        {
            var item = TransaksiList.FirstOrDefault(x => x.Id == transactionId);
            if (item != null) TransaksiList.Remove(item);
        }

    }
}
