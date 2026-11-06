using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClassFinance.Models;

namespace ClassFinance.Services
{
    public static class JadwalTagihanMingguanService
    {
        public static async Task<List<Tagihan>> PastikanMingguTerbuatAsync(int kelasId, DateTime today)
        {
            var store = DataStore.Instance;
            var schedule = store.JadwalTagihanMingguanList.FirstOrDefault(x => x.KelasId == kelasId);
            if (schedule == null)
                return new List<Tagihan>();

            var firstWeek = MondayOf(schedule.StartsOn);
            var currentWeek = MondayOf(today);
            var lastWeek = schedule.EndsOn.HasValue
                ? Min(currentWeek, MondayOf(schedule.EndsOn.Value))
                : currentWeek;

            // Keep out-of-range occurrences in the database as archived records so their
            // payment history stays intact. If the start date moves back later, reuse them.
            bool hasChanges = false;
            var scheduleBills = store.TagihanList
                .Where(bill => bill.WeeklyScheduleId == schedule.Id && bill.PeriodStart.HasValue)
                .ToList();
            foreach (var bill in scheduleBills)
            {
                var billWeek = MondayOf(bill.PeriodStart.Value);
                if (billWeek < firstWeek && !bill.IsArchived)
                {
                    bill.IsArchived = true;
                    hasChanges = true;
                }
                else if (billWeek >= firstWeek && billWeek <= lastWeek && bill.IsArchived)
                {
                    bill.IsArchived = false;
                    hasChanges = true;
                }
            }

            if (!schedule.IsActive || firstWeek > lastWeek ||
                schedule.EndsOn.HasValue && schedule.EndsOn.Value.Date < schedule.StartsOn.Date)
            {
                if (hasChanges)
                    await store.SaveChangesAsync();
                return new List<Tagihan>();
            }

            var created = new List<Tagihan>();
            for (var weekStart = firstWeek; weekStart <= lastWeek; weekStart = weekStart.AddDays(7))
            {
                if (scheduleBills.Any(bill => MondayOf(bill.PeriodStart.Value) == weekStart))
                    continue;

                var bill = new Tagihan
                {
                    Id = store.NextTagihanId(),
                    KelasId = schedule.KelasId,
                    Name = schedule.Name,
                    Amount = schedule.Amount,
                    CreatedDate = DateTime.Now,
                    WeeklyScheduleId = schedule.Id,
                    PeriodStart = weekStart,
                    PeriodEnd = weekStart.AddDays(6)
                };
                store.TagihanList.Add(bill);
                scheduleBills.Add(bill);
                bill.GenerateTagihanSiswa(store);
                created.Add(bill);
            }

            if (created.Count == 0 && !hasChanges)
                return created;

            if (created.Count == 0)
            {
                await store.SaveChangesAsync();
                return created;
            }

            // Apply any stored student credit to the oldest open balances, just as when a bill is created manually.
            var systemRecorder = new Bendahara { Name = "KasMate" };
            systemRecorder.ApplyStoredCreditForClass(kelasId, DateTime.Now);

            var current = created.Where(x => x.PeriodStart?.Date == currentWeek).ToList();
            foreach (var bill in current)
                Notifikasi.TambahkanUntukTagihanBaru(kelasId, bill.Id);

            await store.SaveChangesAsync();

            return created;
        }

        public static DateTime MondayOf(DateTime date)
        {
            var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
            return date.Date.AddDays(-daysSinceMonday);
        }

        public static int GetWeekNumber(JadwalTagihanMingguan schedule, DateTime periodStart)
        {
            if (schedule == null)
                return 1;

            var firstWeek = MondayOf(schedule.StartsOn);
            var currentWeek = MondayOf(periodStart);
            if (currentWeek < firstWeek)
                return 0;

            return (int)((currentWeek - firstWeek).TotalDays / 7) + 1;
        }

        public static int GetWeekNumber(Tagihan bill)
        {
            if (bill?.WeeklyScheduleId == null || !bill.PeriodStart.HasValue)
                return 1;

            var schedule = DataStore.Instance.JadwalTagihanMingguanList
                .FirstOrDefault(x => x.Id == bill.WeeklyScheduleId.Value);
            return GetWeekNumber(schedule, bill.PeriodStart.Value);
        }

        private static DateTime Min(DateTime left, DateTime right) => left <= right ? left : right;
    }
}
