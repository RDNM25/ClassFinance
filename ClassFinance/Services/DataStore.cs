using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using ClassFinance.Models;

namespace ClassFinance.Services
{
    /// <summary>Application data loaded from and persisted to the local MariaDB database.</summary>
    public sealed class DataStore : INotifyPropertyChanged
    {
        private static readonly Lazy<DataStore> _instance = new(() => new DataStore());
        public static DataStore Instance => _instance.Value;

        private readonly MariaDbDataRepository _repository = new();
        private bool _initialized;

        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<Kelas> KelasList { get; } = new();
        public ObservableCollection<Tagihan> TagihanList { get; } = new();
        public ObservableCollection<TagihanSiswa> TagihanSiswaList { get; } = new();
        public ObservableCollection<Transaksi> TransaksiList { get; } = new();
        public ObservableCollection<Notifikasi> NotifikasiList { get; } = new();

        public int TransactionRevision { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler DataChanged;

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
            TransaksiList.CollectionChanged += TransaksiList_CollectionChanged;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _repository.Load(this);
            _initialized = true;
        }

        public void SaveChanges()
        {
            if (!_initialized) throw new InvalidOperationException("Database has not been initialized.");
            _repository.Save(this);
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void SetNextIds()
        {
            _userId = Math.Max(Users.Select(u => u.Id).DefaultIfEmpty(0).Max(), Users.OfType<Siswa>().Select(s => s.Id).DefaultIfEmpty(0).Max()) + 1;
            _kelasId = KelasList.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
            _tagihanId = TagihanList.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
            _tagihanSiswaId = TagihanSiswaList.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
            _transaksiId = TransaksiList.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
            _notifId = NotifikasiList.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
        }

        private void TransaksiList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            TransactionRevision++;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransactionRevision)));
        }

        public Transaksi TambahTransaksi(int kelasId, JenisTransaksi type, decimal amount, DateTime date,
            string description, int? tagihanSiswaId, string createdBy, string proofFile = null, int? siswaId = null, bool affectsKas = true)
        {
            var t = new Transaksi
            {
                Id = NextTransaksiId(), KelasId = kelasId, Type = type, Amount = amount,
                Date = date, Description = description, TagihanSiswaId = tagihanSiswaId,
                CreatedBy = createdBy, ProofFile = proofFile, SiswaId = siswaId, AffectsKas = affectsKas
            };
            TransaksiList.Add(t);
            SaveChanges();
            return t;
        }

        public Tagihan BuatTagihan(int kelasId, string name, decimal amount, DateTime? createdDate = null)
        {
            var tagihan = new Tagihan
            {
                Id = NextTagihanId(), KelasId = kelasId, Name = name,
                Amount = amount, CreatedDate = createdDate ?? DateTime.Now
            };
            TagihanList.Add(tagihan);
            tagihan.GenerateTagihanSiswa(this);
            SaveChanges();
            return tagihan;
        }

        public void HapusSiswa(int siswaId)
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
            _repository.DeleteStudent(siswaId);
            Users.Remove(siswa);
            entries.ForEach(x => TagihanSiswaList.Remove(x));
            NotifikasiList.Where(x => x.UserId == siswaId).ToList().ForEach(x => NotifikasiList.Remove(x));
            SaveChanges();
        }

        public void DeleteTransaction(int transactionId)
        {
            _repository.DeleteTransaction(transactionId);
            var item = TransaksiList.FirstOrDefault(x => x.Id == transactionId);
            if (item != null) TransaksiList.Remove(item);
        }

        public void DeleteBill(int billId)
        {
            _repository.DeleteBill(billId);
            var entryIds = TagihanSiswaList.Where(x => x.TagihanId == billId).Select(x => x.Id).ToHashSet();
            foreach (var transaction in TransaksiList.Where(x => x.TagihanSiswaId.HasValue && entryIds.Contains(x.TagihanSiswaId.Value)))
                transaction.TagihanSiswaId = null;
            foreach (var notification in NotifikasiList.Where(x => x.BillId == billId))
                notification.BillId = null;
            TagihanSiswaList.Where(x => x.TagihanId == billId).ToList().ForEach(x => TagihanSiswaList.Remove(x));
            TagihanList.Remove(TagihanList.FirstOrDefault(x => x.Id == billId));
            SaveChanges();
        }
    }
}
