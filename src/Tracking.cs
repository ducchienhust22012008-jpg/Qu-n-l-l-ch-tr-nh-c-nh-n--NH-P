using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Nhip {
    public class TimeSlice { public long Start { get; set; } public long End { get; set; } }
    public class FocusRun {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public long Created { get; set; }
        public long Anchor { get; set; }
        public bool Running { get; set; }
        public List<TimeSlice> Slices { get; set; }
    }
    public class FocusSession {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public long Created { get; set; }
        public long Finished { get; set; }
        public List<TimeSlice> Slices { get; set; }
    }
    public class StatsBucket {
        public DateTime From, To;
        public int Planned, Completed, Goals, GoalsDone;
        public double StudySeconds, WorkSeconds, EarnedMinutes, RevokedMinutes, EntertainmentMinutes;
        public double FocusSeconds { get { return StudySeconds + WorkSeconds; } }
    }
    public partial class Store {
        static void ValidateSlices(List<TimeSlice> slices) {
            if (slices == null || slices.Any(s => s == null || s.Start <= 0 || s.End < s.Start || s.End > DateTime.MaxValue.Ticks)) throw new InvalidDataException("Dữ liệu phiên bấm giờ không hợp lệ.");
        }
        static void ValidateTracking(AppData data) {
            if (data.FocusSessions == null) data.FocusSessions = new List<FocusSession>();
            foreach (var session in data.FocusSessions) { if (session == null || String.IsNullOrWhiteSpace(session.Title)) throw new InvalidDataException("Phiên bấm giờ không hợp lệ."); ValidateSlices(session.Slices); }
            var active = data.ActiveFocus;
            if (active != null) {
                if (String.IsNullOrWhiteSpace(active.Title) || active.Anchor <= 0 || active.Anchor > DateTime.MaxValue.Ticks) throw new InvalidDataException("Bộ bấm giờ không hợp lệ.");
                ValidateSlices(active.Slices);
            }
        }
        public static double SliceSeconds(IEnumerable<TimeSlice> slices) { return slices.Sum(s => (s.End - s.Start) / (double)TimeSpan.TicksPerSecond); }
        public double FocusSeconds() {
            var active = Data.ActiveFocus; if (active == null) return 0;
            return SliceSeconds(active.Slices) + (active.Running ? Math.Max(0, (Clock().Ticks - active.Anchor) / (double)TimeSpan.TicksPerSecond) : 0);
        }
        void SettleFocus() {
            var active = Data.ActiveFocus;
            if (active == null || !active.Running) return;
            long end = Math.Max(active.Anchor, Clock().Ticks);
            if (end > active.Anchor) {
                var last = active.Slices.LastOrDefault();
                if (last != null && last.End == active.Anchor) last.End = end;
                else active.Slices.Add(new TimeSlice { Start = active.Anchor, End = end });
            }
            active.Anchor = end;
        }
        public void StartFocus(string title, string category) {
            if (String.IsNullOrWhiteSpace(title) || title.Trim().Length > 160 || (category != "Học tập" && category != "Công việc")) throw new InvalidDataException("Nhập tên phiên và chọn Học tập hoặc Công việc.");
            Commit(() => {
                if (Data.ActiveFocus != null) throw new InvalidOperationException("Hãy lưu phiên hiện tại trước khi bắt đầu phiên mới.");
                Data.Running = false;
                Data.ActiveFocus = new FocusRun { Id = Guid.NewGuid().ToString("N"), Title = title.Trim(), Category = category, Created = Clock().Ticks, Anchor = Clock().Ticks, Running = true, Slices = new List<TimeSlice>() };
            });
        }
        public void PauseFocus() { Commit(() => { if (Data.ActiveFocus != null) Data.ActiveFocus.Running = false; }); }
        public void ResumeFocus() { Commit(() => {
            if (Data.ActiveFocus == null) throw new InvalidOperationException("Chưa có phiên bấm giờ.");
            Data.Running = false; Data.ActiveFocus.Running = true; Data.ActiveFocus.Anchor = Clock().Ticks;
        }); }
        public void FinishFocus() { Commit(() => {
            var active = Data.ActiveFocus; if (active == null) throw new InvalidOperationException("Chưa có phiên để lưu.");
            if (SliceSeconds(active.Slices) < 1) throw new InvalidOperationException("Hãy bấm giờ ít nhất một giây trước khi lưu.");
            Data.FocusSessions.Add(new FocusSession { Id = active.Id, Title = active.Title, Category = active.Category, Created = active.Created, Finished = Clock().Ticks, Slices = active.Slices });
            Data.ActiveFocus = null;
        }); }
        public void DiscardFocus() { Commit(() => { Data.ActiveFocus = null; }); }
        public void CheckpointFocus() { if (Data.ActiveFocus != null && Data.ActiveFocus.Running) Commit(() => { }); }
        public bool RecoverInterruptedFocus() {
            if (Data.ActiveFocus == null || !Data.ActiveFocus.Running) return false;
            Commit(() => { Data.ActiveFocus.Running = false; }, false); return true;
        }
        static DateTime Utc(long ticks) { return new DateTime(ticks, DateTimeKind.Utc); }
        static double Overlap(IEnumerable<TimeSlice> slices, DateTime from, DateTime to) {
            long start = from.ToUniversalTime().Ticks, end = to.ToUniversalTime().Ticks;
            return slices.Sum(s => Math.Max(0, Math.Min(end, s.End) - Math.Max(start, s.Start)) / (double)TimeSpan.TicksPerSecond);
        }
        public List<StatsBucket> Statistics(DateTime from, DateTime toExclusive, string grouping) {
            from = from.Date; toExclusive = toExclusive.Date;
            if (toExclusive <= from || toExclusive.Year - from.Year > 100) throw new InvalidDataException("Chọn khoảng ngày hợp lệ, tối đa 100 năm.");
            var slices = Data.FocusSessions.Select(s => new { s.Category, Slices = (IEnumerable<TimeSlice>)s.Slices }).ToList();
            if (Data.ActiveFocus != null) {
                var current = Data.ActiveFocus.Slices.ToList();
                if (Data.ActiveFocus.Running && Clock().Ticks > Data.ActiveFocus.Anchor) current.Add(new TimeSlice { Start = Data.ActiveFocus.Anchor, End = Clock().Ticks });
                slices.Add(new { Data.ActiveFocus.Category, Slices = (IEnumerable<TimeSlice>)current });
            }
            var buckets = new List<StatsBucket>(); DateTime cursor = from;
            while (cursor < toExclusive) {
                DateTime end = grouping == "Tháng" ? new DateTime(cursor.Year, cursor.Month, 1).AddMonths(1) : grouping == "Tuần" ? WeekOf(cursor).AddDays(7) : cursor.AddDays(1);
                if (end > toExclusive) end = toExclusive;
                string first = cursor.ToString("yyyy-MM-dd"), last = end.ToString("yyyy-MM-dd");
                var tasks = Data.Tasks.Where(t => String.CompareOrdinal(t.Day, first) >= 0 && String.CompareOrdinal(t.Day, last) < 0).ToList();
                var bucket = new StatsBucket { From = cursor, To = end, Planned = tasks.Count, Completed = tasks.Count(t => t.Done) };
                foreach (var source in slices) { double seconds = Overlap(source.Slices, cursor, end); if (source.Category == "Học tập") bucket.StudySeconds += seconds; else bucket.WorkSeconds += seconds; }
                foreach (var entry in Data.History) {
                    DateTime at; if (!DateTime.TryParse(entry.At, null, DateTimeStyles.RoundtripKind, out at)) continue; at = at.ToLocalTime();
                    if (entry.Detail == "Giải trí" && entry.Seconds < 0) {
                        // Legacy recordings store settlement time. Allocate the elapsed interval rather than counting every minute on its last day.
                        DateTime usageEnd = at; DateTime usageStart = at.AddSeconds(entry.Seconds);
                        bucket.EntertainmentMinutes += Math.Max(0, (Min(end, usageEnd) - Max(cursor, usageStart)).TotalMinutes);
                    } else if (at >= cursor && at < end) {
                        if (entry.Seconds > 0) bucket.EarnedMinutes += entry.Seconds / 60;
                        else if (entry.Seconds < 0) bucket.RevokedMinutes -= entry.Seconds / 60;
                    }
                }
                if (Data.Running && CurrentBalance() < Data.Balance) {
                    DateTime usageStart = Utc(Data.AnchorTicks).ToLocalTime(), usageEnd = usageStart.AddSeconds(Data.Balance - CurrentBalance());
                    bucket.EntertainmentMinutes += Math.Max(0, (Min(end, usageEnd) - Max(cursor, usageStart)).TotalMinutes);
                }
                // Monthly goals appear in the bucket containing the month's last selected day, once per month.
                foreach (var goal in Data.Goals) {
                    DateTime month = DateTime.ParseExact(goal.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    DateTime marker = Min(month.AddMonths(1), toExclusive).AddTicks(-1);
                    if (marker >= cursor && marker < end && month < toExclusive && month.AddMonths(1) > from) { bucket.Goals++; if (GoalIsDone(goal)) bucket.GoalsDone++; }
                }
                buckets.Add(bucket); cursor = end;
            }
            return buckets;
        }
        static DateTime Min(DateTime a, DateTime b) { return a < b ? a : b; }
        static DateTime Max(DateTime a, DateTime b) { return a > b ? a : b; }
        public static DateTime WeekOf(DateTime date) { return date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7)); }
    }
}
