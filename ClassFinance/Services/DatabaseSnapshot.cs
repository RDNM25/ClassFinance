using ClassFinance.Models;

namespace ClassFinance.Services;

internal sealed record ClassRow(int Id, string Name, string AcademicYear);
internal sealed record ScheduleRow(int Id, int ClassId, string Name, decimal Amount, DateTime StartsOn,
    DateTime? EndsOn, bool IsActive, DateTime CreatedAt);
internal sealed record StaffRow(int Id, string Name, string Username, Role Role, int? ClassId);
internal sealed record StudentRow(int Id, string Name, string Nis, int ClassId, decimal Balance);
internal sealed record BillRow(int Id, int ClassId, string Name, decimal Amount, DateTime CreatedAt,
    int? ScheduleId, DateTime? PeriodStart, DateTime? PeriodEnd, bool IsArchived);
internal sealed record BillStudentRow(int Id, int BillId, int StudentId, StatusTagihan Status,
    decimal AmountDue, decimal AmountPaid, decimal AssessedAmount);
internal sealed record TransactionRow(int Id, int ClassId, JenisTransaksi Type, MetodeTransaksi Method,
    decimal Amount, string Description, int? StudentId, int? BillStudentId, string CreatedBy,
    DateTime CreatedAt, bool AffectsKas, bool IsRefund, bool IsPendingRefund);
internal sealed record NotificationRow(int Id, int StudentId, int? BillId, string Title, string Message,
    bool IsRead, DateTime CreatedAt);

internal sealed record TableChanges<T>(IReadOnlyList<T> Upserts, IReadOnlyList<int> Deletes)
{
    public bool IsEmpty => Upserts.Count == 0 && Deletes.Count == 0;
}

internal sealed record DatabaseChanges(
    TableChanges<ClassRow> Classes,
    TableChanges<ScheduleRow> Schedules,
    TableChanges<StaffRow> Staff,
    TableChanges<StudentRow> Students,
    TableChanges<BillRow> Bills,
    TableChanges<BillStudentRow> BillStudents,
    TableChanges<TransactionRow> Transactions,
    TableChanges<NotificationRow> Notifications)
{
    public bool IsEmpty => Classes.IsEmpty && Schedules.IsEmpty && Staff.IsEmpty && Students.IsEmpty &&
        Bills.IsEmpty && BillStudents.IsEmpty && Transactions.IsEmpty && Notifications.IsEmpty;
}

internal sealed record DatabaseSnapshot(
    IReadOnlyDictionary<int, ClassRow> Classes,
    IReadOnlyDictionary<int, ScheduleRow> Schedules,
    IReadOnlyDictionary<int, StaffRow> Staff,
    IReadOnlyDictionary<int, StudentRow> Students,
    IReadOnlyDictionary<int, BillRow> Bills,
    IReadOnlyDictionary<int, BillStudentRow> BillStudents,
    IReadOnlyDictionary<int, TransactionRow> Transactions,
    IReadOnlyDictionary<int, NotificationRow> Notifications)
{
    public static DatabaseSnapshot Capture(DataStore store) => new(
        store.KelasList.ToDictionary(item => item.Id,
            item => new ClassRow(item.Id, item.Name, item.AcademicYear)),
        store.JadwalTagihanMingguanList.ToDictionary(item => item.Id,
            item => new ScheduleRow(item.Id, item.KelasId, item.Name, item.Amount, item.StartsOn.Date,
                item.EndsOn?.Date, item.IsActive, item.CreatedDate)),
        store.Users.Where(item => item is not Siswa).ToDictionary(item => item.Id,
            item => new StaffRow(item.Id, item.Name, item.Username, item.Role,
                item is WaliKelas wali ? wali.KelasId : null)),
        store.Users.OfType<Siswa>().ToDictionary(item => item.Id,
            item => new StudentRow(item.Id, item.Name, item.Nis, item.KelasId, item.SaldoTitipan)),
        store.TagihanList.ToDictionary(item => item.Id,
            item => new BillRow(item.Id, item.KelasId, item.Name, item.Amount, item.CreatedDate,
                item.WeeklyScheduleId, item.PeriodStart?.Date, item.PeriodEnd?.Date, item.IsArchived)),
        store.TagihanSiswaList.ToDictionary(item => item.Id,
            item => new BillStudentRow(item.Id, item.TagihanId, item.SiswaId, item.Status,
                item.AmountDue, item.JumlahDibayar, item.AssessedAmount)),
        store.TransaksiList.ToDictionary(item => item.Id,
            item => new TransactionRow(item.Id, item.KelasId, item.Type, item.Metode, item.Amount,
                item.Description, item.SiswaId, item.TagihanSiswaId, item.CreatedBy, item.Date,
                item.AffectsKas, item.IsRefund, item.IsPendingRefund)),
        store.NotifikasiList.ToDictionary(item => item.Id,
            item => new NotificationRow(item.Id, item.UserId, item.BillId, item.Title, item.Message,
                item.IsRead, item.CreatedAt)));

    public DatabaseChanges Diff(DatabaseSnapshot current) => new(
        DiffTable(Classes, current.Classes),
        DiffTable(Schedules, current.Schedules),
        DiffTable(Staff, current.Staff),
        DiffTable(Students, current.Students),
        DiffTable(Bills, current.Bills),
        DiffTable(BillStudents, current.BillStudents),
        DiffTable(Transactions, current.Transactions),
        DiffTable(Notifications, current.Notifications));

    private static TableChanges<T> DiffTable<T>(IReadOnlyDictionary<int, T> before,
        IReadOnlyDictionary<int, T> after)
    {
        var upserts = after.Where(pair => !before.TryGetValue(pair.Key, out var oldRow) ||
                                          !EqualityComparer<T>.Default.Equals(oldRow, pair.Value))
            .Select(pair => pair.Value)
            .ToList();
        var deletes = before.Keys.Where(id => !after.ContainsKey(id)).ToList();
        return new TableChanges<T>(upserts, deletes);
    }
}
