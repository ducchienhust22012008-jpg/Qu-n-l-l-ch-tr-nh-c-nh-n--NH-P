using System;
using System.IO;
using System.Linq;
using Nhip;

class Tests {
    static int count;
    static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
    static TaskItem Item(string id, int reward) { return new TaskItem { Id = id, Title = "Học Toán tiếng Việt", Day = "2026-10-22", Time = "09:00", Duration = 60, Category = "Học tập", Reward = reward }; }
    static void Main(string[] args) {
        try {
            string root = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-data-" + DateTime.UtcNow.Ticks);
            var now = new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc);
            var s = new Store(root); s.Clock = () => now;
            Check(s.Data.Tasks.Count == 0 && s.CurrentBalance() == 0, "fresh install has no demo tasks or free credits");
            s.Upsert(Item("a", 15)); s.SetDone("a", true);
            Check(s.CurrentBalance() == 900, "completion awards 15 minutes");
            s.SetDone("a", true); Check(s.CurrentBalance() == 900, "duplicate completion cannot award twice");
            s.Start(); now = now.AddSeconds(37); Check(s.CurrentBalance() == 863, "active countdown subtracts elapsed wall time");
            var reopened = new Store(root); reopened.Clock = () => now;
            Check(reopened.Data.Running && reopened.CurrentBalance() == 863, "crash/reopen resumes from persisted timestamp");
            s.Pause(); now = now.AddHours(1); Check(s.CurrentBalance() == 863, "pause does not consume credits");
            s = new Store(root); s.Clock = () => now; Check(!s.Data.Running && s.CurrentBalance() == 863, "clean close/reopen preserves paused balance");
            var changed = Item("a", 30); changed.Title = "Tên việc được sửa"; s.Upsert(changed);
            Check(s.Data.Tasks[0].Awarded == 15 && s.CurrentBalance() == 863, "editing completed task cannot mint extra reward");
            s.SetDone("a", false); Check(s.CurrentBalance() == -37, "undo spent reward creates exact debt");
            bool rejected = false; try { s.Start(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !s.Data.Running && s.CurrentBalance() == -37, "negative balance cannot start timer and transaction rolls back");
            s.SetDone("a", true); Check(s.CurrentBalance() == 1763, "new completion uses edited reward and repays debt");
            s.Start(); now = now.AddSeconds(5000); s.Expire();
            Check(!s.Data.Running && s.CurrentBalance() == 0, "expiry stops exactly at zero after sleep or long delay");
            s.SetDone("a", false); s.Upsert(Item("b", 40)); s.SetDone("b", true);
            Check(s.CurrentBalance() == 600, "future credits repay debt before becoming spendable");
            s.Delete("b"); Check(s.CurrentBalance() == -1800 && !s.Data.Tasks.Any(t => t.Id == "b"), "deleting completed task revokes awarded minutes");
            s.Upsert(Item("zero", 0)); s.SetDone("zero", true); Check(s.CurrentBalance() == -1800, "zero reward task is supported");
            bool bad = false; var invalid = Item("bad", -1); try { s.Upsert(invalid); } catch (InvalidDataException) { bad = true; }
            Check(bad && !s.Data.Tasks.Any(t => t.Id == "bad"), "invalid input rejected");
            var saved = new Store(root); Check(saved.Data.Tasks.Any(t => t.Title.Contains("sửa")), "Vietnamese text survives persistence");
            double before = s.CurrentBalance();
            Directory.CreateDirectory(s.FilePath + ".tmp");
            bool failed = false; try { s.SetDone("zero", false); } catch (UnauthorizedAccessException) { failed = true; } catch (IOException) { failed = true; }
            Check(failed && s.CurrentBalance() == before && s.Data.Tasks.First(t => t.Id == "zero").Done, "disk write failure rolls back task and balance");
            Directory.Delete(s.FilePath + ".tmp");
            File.WriteAllText(s.FilePath, "{broken"); var recovered = new Store(root);
            Check(recovered.Recovered && Directory.GetFiles(root, "*.damaged-*").Length == 1, "corrupt primary recovers backup and preserves evidence");
            string noBackup = Path.Combine(root, "no-backup"); Directory.CreateDirectory(noBackup); File.WriteAllText(Path.Combine(noBackup, "nhip.json"), "broken");
            bool refused = false; try { new Store(noBackup); } catch (IOException) { refused = true; }
            Check(refused && File.ReadAllText(Path.Combine(noBackup, "nhip.json")) == "broken", "unrecoverable data is never silently reset");
            var planner = new Store(Path.Combine(root, "planning"));
            planner.Upsert(Item("plan", 12)); planner.SetDone("plan", true);
            double planBalance = planner.CurrentBalance();
            planner.SaveSettings(25, new System.Collections.Generic.Dictionary<string, int> { { "plan", 40 } });
            Check(planner.Data.Settings.DefaultReward == 25 && planner.Data.Tasks[0].Reward == 40 && planner.Data.Tasks[0].Awarded == 12 && planner.CurrentBalance() == planBalance, "settings update rewards without re-awarding completed task");
            planner.Reschedule("plan", "2026-10-24", "23:45", 90);
            Check(planner.Data.Tasks[0].Day == "2026-10-24" && planner.Data.Tasks[0].Time == "23:45" && planner.Data.Tasks[0].Duration == 90 && planner.Data.Tasks[0].Done && planner.CurrentBalance() == planBalance, "reschedule supports midnight crossing and preserves completion and balance");
            bool invalidTime = false; try { planner.Reschedule("plan", "2026-10-24", "24:15", 90); } catch (InvalidDataException) { invalidTime = true; }
            Check(invalidTime && planner.Data.Tasks[0].Time == "23:45", "invalid drop time cannot corrupt original schedule");
            planner.SetDone("plan", false); Check(planner.CurrentBalance() == 0, "undo after settings change revokes original award");
            planner.SetDone("plan", true); Check(planner.CurrentBalance() == 2400, "next completion uses per-task setting");
            var goals = new System.Collections.Generic.List<MonthlyGoal> { new MonthlyGoal { Id = "g1", Month = "2026-10", Title = "Đọc sách", Current = 8, Target = 20, Unit = "trang" } };
            planner.SaveGoals("2026-10", goals);
            planner.SaveGoals("2026-11", new System.Collections.Generic.List<MonthlyGoal> { new MonthlyGoal { Id = "g2", Month = "2026-11", Title = "Đi bộ", Current = 20, Target = 20, Unit = "ngày" } });
            Check(planner.Data.Goals.Count == 2 && planner.CurrentBalance() == 2400, "goals remain independent by month and do not mint timer rewards");
            goals[0].Current = 20; planner.SaveGoals("2026-10", goals);
            Check(planner.Data.Goals.First(g => g.Id == "g1").Current == 20 && planner.Data.Goals.Count == 2, "editing monthly goals retains other months");
            planner.SaveGoals("2026-10", new System.Collections.Generic.List<MonthlyGoal>());
            Check(planner.Data.Goals.Count == 1 && planner.Data.Goals[0].Month == "2026-11", "deleting monthly goals retains other months");
            var reloadedPlan = new Store(Path.Combine(root, "planning"));
            Check(reloadedPlan.Data.Goals.Count == 1 && reloadedPlan.Data.Settings.DefaultReward == 25 && reloadedPlan.Data.Tasks[0].Time == "23:45", "new planning data survives disk round trip");
            bool badSettings = false; try { planner.SaveSettings(601, new System.Collections.Generic.Dictionary<string, int>()); } catch (InvalidDataException) { badSettings = true; }
            Check(badSettings && planner.Data.Settings.DefaultReward == 25, "invalid settings rejected without changes");
            goals[0].Target = 0; bool badGoal = false; try { planner.SaveGoals("2026-10", goals); } catch (InvalidDataException) { badGoal = true; }
            Check(badGoal && planner.Data.Goals.Count == 1, "invalid goal does not overwrite saved goals");
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            var oldDocument = serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(planner.FilePath)); oldDocument.Remove("Settings"); oldDocument.Remove("Goals");
            string legacy = Path.Combine(root, "legacy"); Directory.CreateDirectory(legacy); File.WriteAllText(Path.Combine(legacy, "nhip.json"), serializer.Serialize(oldDocument));
            var migrated = new Store(legacy);
            Check(migrated.Data.Tasks.Count == 1 && migrated.CurrentBalance() == 2400 && migrated.Data.Settings.DefaultReward == 15 && migrated.Data.Goals.Count == 0, "version 1 data loads with safe defaults and preserves original tasks and credits");
            Check(Store.GoalIsDone(planner.Data.Goals[0]), "legacy completed goal retains its existing completion state");
            planner.SetGoalDone("g2", false);
            Check(!Store.GoalIsDone(planner.Data.Goals[0]) && planner.Data.Goals[0].Current == 20, "unchecking a goal overrides legacy numeric completion without deleting progress");
            planner.SetGoalDone("g2", true);
            Check(Store.GoalIsDone(planner.Data.Goals[0]) && planner.CurrentBalance() == 2400, "goal tick completes without granting entertainment credits");
            planner.SetTaskColor("plan", "#C14687");
            var draft = Store.CopyDraft(planner.Data.Tasks.First(t => t.Id == "plan"));
            Check(draft.Id != "plan" && !draft.Done && draft.Awarded == 0 && draft.ColorHex == "#C14687" && draft.Reward == 40 && draft.Duration == 90, "copy draft retains appearance and schedule but resets completion and award");
            Check(planner.Data.Tasks.Count == 1 && planner.CurrentBalance() == 2400, "opening a copy draft does not save it or change credits");
            planner.Upsert(draft);
            Check(planner.Data.Tasks.Count == 2 && planner.CurrentBalance() == 2400, "saving task copy creates independent task without instant reward");
            planner.Reschedule(draft.Id, "2026-10-25", "13:30", 45);
            Check(planner.Data.Tasks.First(t => t.Id == draft.Id).ColorHex == "#C14687" && planner.Data.Tasks.First(t => t.Id == "plan").Time == "23:45", "moving a copy retains custom color and leaves source untouched");
            bool badColor = false; try { planner.SetTaskColor(draft.Id, "bad-color"); } catch (InvalidDataException) { badColor = true; }
            Check(badColor && planner.Data.Tasks.First(t => t.Id == draft.Id).ColorHex == "#C14687", "invalid color cannot overwrite saved appearance");
            var colorReloaded = new Store(Path.Combine(root, "planning"));
            Check(colorReloaded.Data.Tasks.Last().ColorHex == "#C14687" && Store.GoalIsDone(colorReloaded.Data.Goals[0]), "task colors and goal ticks survive persistence");
            planner.SetTaskColor(draft.Id, null); Check(planner.Data.Tasks.First(t => t.Id == draft.Id).ColorHex == null, "task color can reset to category default");
            var quoteMonday = new DateTime(2026, 10, 19);
            Check(WeeklyMotivation.ForWeek(quoteMonday) == WeeklyMotivation.ForWeek(quoteMonday.AddDays(6)) && WeeklyMotivation.ForWeek(quoteMonday) != WeeklyMotivation.ForWeek(quoteMonday.AddDays(7)), "weekly quote is stable within week and changes next week");
            Check(!String.IsNullOrWhiteSpace(WeeklyMotivation.ForWeek(new DateTime(2025, 12, 29))), "weekly quote works across year boundaries");
            TrackingTests.Run(Check, root);
            DeadlineTests.Run(Check, root);
            DailyReportTests.Run(Check, root);
            DeadlineSchedulingTests.Run(Check, root);
            Console.WriteLine("ALL " + count + " CHECKS PASSED");
        } catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
    }
}
