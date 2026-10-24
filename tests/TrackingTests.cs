using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nhip;

static class TrackingTests {
    public static void Run(Action<bool, string> check, string root) {
        string folder = Path.Combine(root, "tracking"); var now = new DateTime(2026, 10, 21, 23, 50, 0, DateTimeKind.Local).ToUniversalTime();
        var s = new Store(folder); s.Clock = () => now;
        s.StartFocus("Ôn tập", "Học tập"); now = now.AddMinutes(20);
        check(s.FocusSeconds() == 1200, "focus stopwatch measures elapsed real time");
        s.PauseFocus(); now = now.AddHours(1);
        check(s.FocusSeconds() == 1200, "focus pause excludes break time");
        s.ResumeFocus(); now = now.AddMinutes(10); s.FinishFocus();
        check(s.Data.FocusSessions.Count == 1 && Store.SliceSeconds(s.Data.FocusSessions[0].Slices) == 1800 && s.Data.ActiveFocus == null && s.CurrentBalance() == 0, "saving study session excludes breaks and does not award credits");
        var days = s.Statistics(new DateTime(2026, 10, 21), new DateTime(2026, 10, 23), "Ngày");
        check(days.Count == 2 && days[0].StudySeconds == 600 && days[1].StudySeconds == 1200, "statistics split real study across local midnight");
        bool duplicate = false; try { s.FinishFocus(); } catch (InvalidOperationException) { duplicate = true; }
        check(duplicate && s.Data.FocusSessions.Count == 1, "session cannot be saved twice");
        s.Upsert(new TaskItem { Id = "tracked", Title = "Việc đã lưu", Day = "2026-10-22", Time = "09:00", Duration = 60, Reward = 30, Category = "Học tập" }); s.SetDone("tracked", true);
        s.Start(); now = now.AddMinutes(5); s.StartFocus("Làm việc", "Công việc");
        check(!s.Data.Running && s.CurrentBalance() == 1500, "starting focus settles and pauses entertainment timer");
        now = now.AddMinutes(1); s.CheckpointFocus(); now = now.AddHours(1);
        var recovered = new Store(folder); recovered.Clock = () => now;
        check(recovered.RecoverInterruptedFocus() && !recovered.Data.ActiveFocus.Running && recovered.FocusSeconds() == 60, "crash recovery keeps last checkpoint and excludes offline time");
        recovered.ResumeFocus(); now = now.AddSeconds(30); recovered.Start();
        check(!recovered.Data.ActiveFocus.Running && recovered.FocusSeconds() == 90 && recovered.Data.Running, "entertainment start pauses study stopwatch");
        recovered.Pause(); recovered.ResumeFocus(); now = now.AddSeconds(30); recovered.FinishFocus();
        var totals = recovered.Statistics(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1), "Tháng").Single();
        check(totals.Completed == 1 && totals.Planned == 1 && totals.EarnedMinutes == 30 && totals.EntertainmentMinutes == 5 && totals.WorkSeconds == 120, "monthly statistics combine scheduled tasks actual focus credits and entertainment");
        recovered.StartFocus("Phiên đang chạy", "Học tập"); now = now.AddSeconds(45);
        double before = recovered.Statistics(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1), "Tháng").Single().FocusSeconds;
        recovered.FinishFocus(); double after = recovered.Statistics(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1), "Tháng").Single().FocusSeconds;
        check(before == after && after == 1965, "active session included once before and after save");
        var opened = new Store(folder); check(opened.Data.FocusSessions.Count == 3 && opened.Data.ActiveFocus == null, "saved focus sessions survive reopen");
        recovered.StartFocus("Không lưu", "Công việc"); now = now.AddSeconds(12); recovered.DiscardFocus();
        check(recovered.Data.ActiveFocus == null && recovered.Data.FocusSessions.Count == 3, "discarding draft does not add a saved session");
        recovered.StartFocus("Lỗi ghi", "Công việc"); now = now.AddSeconds(30); Directory.CreateDirectory(recovered.FilePath + ".tmp");
        bool writeFailed = false; try { recovered.FinishFocus(); } catch (IOException) { writeFailed = true; } catch (UnauthorizedAccessException) { writeFailed = true; }
        check(writeFailed && recovered.Data.ActiveFocus != null && recovered.Data.ActiveFocus.Running && recovered.Data.FocusSessions.Count == 3, "failed focus save rolls back session and retains stopwatch"); Directory.Delete(recovered.FilePath + ".tmp");
        recovered.PauseFocus(); var paused = new Store(folder); paused.Clock = () => now.AddDays(1);
        check(paused.FocusSeconds() == 30 && !paused.RecoverInterruptedFocus(), "normal close pause preserves draft without adding offline hours");
        check(Store.WeekOf(new DateTime(2026, 1, 1)) == new DateTime(2025, 12, 29), "week lookup handles year boundary");
        var empty = new Store(Path.Combine(root, "empty-statistics"));
        check(empty.Statistics(new DateTime(2026, 1, 1), new DateTime(2027, 1, 1), "Tháng").Count == 12, "long term statistics include empty months");
        bool invalid = false; try { empty.Statistics(new DateTime(2026, 10, 23), new DateTime(2026, 10, 22), "Ngày"); } catch (InvalidDataException) { invalid = true; }
        check(invalid, "invalid statistics date range rejected");
    }
}
