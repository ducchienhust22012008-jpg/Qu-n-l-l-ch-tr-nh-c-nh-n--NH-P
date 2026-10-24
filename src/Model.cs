using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Nhip {
    public class TaskItem {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Day { get; set; }
        public string Time { get; set; }
        public int Duration { get; set; }
        public string Category { get; set; }
        public int Reward { get; set; }
        public bool Done { get; set; }
        public int Awarded { get; set; }
        public string ColorHex { get; set; }
    }
    public class Entry {
        public string At { get; set; }
        public string Detail { get; set; }
        public double Seconds { get; set; }
    }
    public class MonthlyGoal {
        public string Id { get; set; }
        public string Month { get; set; }
        public string Title { get; set; }
        public int Current { get; set; }
        public int Target { get; set; }
        public string Unit { get; set; }
        public bool? Done { get; set; }
    }
    public class Preferences {
        public int DefaultReward { get; set; }
        public Preferences() { DefaultReward = 15; }
    }
    public class AppData {
        public int Version { get; set; }
        public List<TaskItem> Tasks { get; set; }
        public List<Entry> History { get; set; }
        public double Balance { get; set; }
        public bool Running { get; set; }
        public long AnchorTicks { get; set; }
        public List<MonthlyGoal> Goals { get; set; }
        public Preferences Settings { get; set; }
        public List<FocusSession> FocusSessions { get; set; }
        public FocusRun ActiveFocus { get; set; }
        public List<Deadline> Deadlines { get; set; }
        public List<TaskItem> UnscheduledTasks { get; set; }
        public AppData() { Deadlines = new List<Deadline>(); UnscheduledTasks = new List<TaskItem>(); Version = 1; Tasks = new List<TaskItem>(); History = new List<Entry>(); Goals = new List<MonthlyGoal>(); Settings = new Preferences(); FocusSessions = new List<FocusSession>(); }
    }
    public partial class Store {
        public AppData Data { get; private set; }
        public string FilePath { get; private set; }
        public bool Recovered { get; private set; }
        private bool preserveBackup;
        private JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 16000000 };
        public Func<DateTime> Clock = () => DateTime.UtcNow;
        public Store(string directory) {
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, "nhip.json");
            if (!File.Exists(FilePath)) {
                if (File.Exists(FilePath + ".bak")) {
                    Data = Read(FilePath + ".bak"); Recovered = true; preserveBackup = true;
                } else Data = new AppData();
            } else {
                try { Data = Read(FilePath); }
                catch (Exception original) {
                    if (!File.Exists(FilePath + ".bak")) throw new IOException("Không đọc được dữ liệu. Tệp gốc được giữ nguyên: " + FilePath, original);
                    Data = Read(FilePath + ".bak");
                    File.Copy(FilePath, FilePath + ".damaged-" + DateTime.UtcNow.Ticks);
                    Recovered = true;
                    preserveBackup = true;
                }
            }
        }
        private AppData Read(string path) {
            AppData d = json.Deserialize<AppData>(File.ReadAllText(path, Encoding.UTF8));
            if (d == null || d.Version != 1 || d.Tasks == null || d.History == null || Double.IsNaN(d.Balance) || Double.IsInfinity(d.Balance)) throw new InvalidDataException("Dữ liệu không hợp lệ hoặc khác phiên bản.");
            foreach (TaskItem t in d.Tasks) Validate(t);
            if (d.Goals == null) d.Goals = new List<MonthlyGoal>();
            if (d.Settings == null) d.Settings = new Preferences();
            if (d.Settings.DefaultReward < 0 || d.Settings.DefaultReward > 600) throw new InvalidDataException("Phút thưởng mặc định không hợp lệ.");
            foreach (MonthlyGoal g in d.Goals) ValidateGoal(g);
            ValidateTracking(d);
            ValidateDeadlines(d);
            if (d.Running && (d.AnchorTicks < DateTime.MinValue.Ticks || d.AnchorTicks > DateTime.MaxValue.Ticks || d.AnchorTicks == 0)) throw new InvalidDataException("Mốc bộ đếm không hợp lệ.");
            return d;
        }
        private static void Validate(TaskItem t) {
            DateTime day; TimeSpan time;
            if (t == null || String.IsNullOrWhiteSpace(t.Id) || String.IsNullOrWhiteSpace(t.Title) || t.Title.Length > 160 || !DateTime.TryParseExact(t.Day, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out day) || !TimeSpan.TryParseExact(t.Time, "hh\\:mm", System.Globalization.CultureInfo.InvariantCulture, out time) || t.Duration < 1 || t.Duration > 1440 || t.Reward < 0 || t.Reward > 600 || t.Awarded < 0 || t.Awarded > 600) throw new InvalidDataException("Thông tin công việc không hợp lệ.");
            ValidateColor(t.ColorHex);
        }
        static void ValidateColor(string color) { if (!String.IsNullOrEmpty(color) && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$")) throw new InvalidDataException("Màu công việc không hợp lệ."); }
        public static bool GoalIsDone(MonthlyGoal goal) { return goal.Done ?? (goal.Current >= goal.Target); }
        public void SetGoalDone(string id, bool done) { Commit(() => { Data.Goals.First(g => g.Id == id).Done = done; }); }
        public void SetTaskColor(string id, string color) { ValidateColor(color); Commit(() => { Data.Tasks.First(t => t.Id == id).ColorHex = color; }); }
        public static TaskItem CopyDraft(TaskItem t) { return new TaskItem { Id = Guid.NewGuid().ToString("N"), Title = t.Title, Day = t.Day, Time = t.Time, Duration = t.Duration, Category = t.Category, Reward = t.Reward, ColorHex = t.ColorHex }; }
        public double CurrentBalance() {
            if (!Data.Running) return Data.Balance;
            double elapsed = Math.Max(0, (Clock().Ticks - Data.AnchorTicks) / (double)TimeSpan.TicksPerSecond);
            return Math.Max(0, Data.Balance - elapsed);
        }
        private void Log(string detail, double seconds) {
            Data.History.Add(new Entry { At = Clock().ToString("o"), Detail = detail, Seconds = seconds });
        }
        private void Settle() {
            if (!Data.Running) return;
            double remaining = CurrentBalance();
            double used = Data.Balance - remaining;
            if (used > 0) Data.History.Add(new Entry { At = new DateTime(Data.AnchorTicks, DateTimeKind.Utc).AddSeconds(used).ToString("o"), Detail = "Giải trí", Seconds = -used });
            Data.Balance = remaining;
            Data.AnchorTicks = Clock().Ticks;
            if (remaining <= 0) Data.Running = false;
        }
        private void Commit(Action change, bool settleFocus = true) {
            string previous = json.Serialize(Data);
            try { Settle(); if (settleFocus) SettleFocus(); change(); Save(); }
            catch { Data = json.Deserialize<AppData>(previous); throw; }
        }
        public void Save() {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json.Serialize(Data));
            string temp = FilePath + ".tmp";
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) {
                fs.Write(bytes, 0, bytes.Length); fs.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, preserveBackup ? null : FilePath + ".bak");
            else File.Move(temp, FilePath);
            preserveBackup = false;
        }
        public void Upsert(TaskItem item) {
            Validate(item);
            Commit(() => {
                TaskItem old = Data.Tasks.FirstOrDefault(t => t.Id == item.Id);
                if (old != null) {
                    item.Done = old.Done; item.Awarded = old.Awarded;
                    Data.Tasks.Remove(old);
                } else { item.Done = false; item.Awarded = 0; }
                Data.Tasks.Add(item);
            });
        }
        private static void ValidateGoal(MonthlyGoal g) {
            DateTime month;
            if (g == null || String.IsNullOrWhiteSpace(g.Id) || String.IsNullOrWhiteSpace(g.Title) || g.Title.Length > 120 || !DateTime.TryParseExact(g.Month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out month) || g.Current < 0 || g.Current > 1000000 || g.Target < 1 || g.Target > 1000000 || (g.Unit != null && g.Unit.Length > 20)) throw new InvalidDataException("Mục tiêu cần tên, chỉ tiêu từ 1 đến 1.000.000 và tiến độ không âm.");
        }
        public void SaveGoals(string month, List<MonthlyGoal> goals) {
            foreach (var g in goals) { ValidateGoal(g); if (g.Month != month) throw new InvalidDataException("Mục tiêu khác tháng."); }
            if (goals.Select(g => g.Id).Distinct().Count() != goals.Count) throw new InvalidDataException("Mục tiêu bị trùng.");
            Commit(() => {
                Data.Goals.RemoveAll(g => g.Month == month);
                Data.Goals.AddRange(goals.Select(g => new MonthlyGoal { Id = g.Id, Month = g.Month, Title = g.Title, Current = g.Current, Target = g.Target, Unit = g.Unit, Done = g.Done }));
            });
        }
        public void SaveSettings(int defaultReward, Dictionary<string, int> rewards) {
            if (defaultReward < 0 || defaultReward > 600 || rewards.Any(x => x.Value < 0 || x.Value > 600)) throw new InvalidDataException("Phút thưởng phải từ 0 đến 600.");
            Commit(() => {
                Data.Settings.DefaultReward = defaultReward;
                foreach (var pair in rewards) Data.Tasks.First(t => t.Id == pair.Key).Reward = pair.Value;
            });
        }
        public void Reschedule(string id, string day, string time, int duration) {
            var t = Data.Tasks.First(x => x.Id == id);
            Upsert(new TaskItem { Id = t.Id, Title = t.Title, Day = day, Time = time, Duration = duration, Category = t.Category, Reward = t.Reward, ColorHex = t.ColorHex });
        }
        public void SetDone(string id, bool done) {
            Commit(() => {
                TaskItem t = AllTasks.First(x => x.Id == id);
                if (t.Done == done) return;
                int minutes = done ? t.Reward : -t.Awarded;
                Data.Balance += minutes * 60;
                Log((done ? "Hoàn thành: " : "Bỏ hoàn thành: ") + t.Title, minutes * 60);
                t.Done = done; t.Awarded = done ? t.Reward : 0;
                if (!done) { var parent = ParentDeadline(id); if (parent != null) parent.CompletedAt = null; }
                if (Data.Balance <= 0) Data.Running = false;
            });
        }
        public void Delete(string id) {
            Commit(() => {
                TaskItem t = AllTasks.First(x => x.Id == id);
                if (t.Done) { Data.Balance -= t.Awarded * 60; Log("Xóa việc đã hoàn thành: " + t.Title, -t.Awarded * 60); }
                Data.Tasks.Remove(t); Data.UnscheduledTasks.Remove(t);
                foreach (var d in Data.Deadlines) { if (d.TaskIds.Remove(id)) d.CompletedAt = null; }
                if (Data.Balance <= 0) Data.Running = false;
            });
        }
        public void Start() {
            Commit(() => {
                if (Data.Balance <= 0) throw new InvalidOperationException("Hoàn thành công việc để nhận thời gian giải trí trước nhé.");
                if (Data.ActiveFocus != null) Data.ActiveFocus.Running = false;
                Data.Running = true; Data.AnchorTicks = Clock().Ticks;
            });
        }
        public void Pause() { Commit(() => { Data.Running = false; }); }
        public void Expire() { if (Data.Running && CurrentBalance() <= 0) Pause(); }
    }
    public static class WeeklyMotivation {
        static readonly string[] Quotes = {
            "Mỗi bước nhỏ đều đưa bạn tiến lên.", "Bền bỉ hôm nay, vững vàng ngày mai.", "Bắt đầu nhỏ, tiến bộ mỗi ngày.", "Giữ nhịp của bạn, đi đến cùng.",
            "Tập trung vào điều bạn có thể làm.", "Một việc xong, thêm một bước tiến.", "Cho mục tiêu một chút thời gian mỗi ngày.", "Đi chậm vẫn là đang tiến về phía trước.",
            "Làm tốt hôm nay, mở lối ngày mai.", "Biến kế hoạch thành bước chân đầu tiên.", "Sự đều đặn tạo nên thay đổi.", "Mỗi lần bắt đầu lại là một cơ hội.",
            "Dành sức cho điều thực sự quan trọng.", "Hành động nhỏ, niềm tin lớn dần.", "Bạn đang xây tương lai từ hôm nay.", "Chăm chút từng ngày, vững bước từng tuần.",
            "Hoàn thành một việc, nhẹ đi một lo.", "Tin vào quá trình bạn đang vun đắp.", "Tiến bộ bắt đầu từ việc bắt tay làm.", "Giữ lời hứa nhỏ với chính mình.",
            "Mỗi tuần là một cơ hội để tiến lên.", "Kiên nhẫn với mình, bền bỉ với mục tiêu.", "Dành thời gian cho điều bạn muốn trở thành.", "Làm từng việc, đi từng bước."
        };
        public static string ForWeek(DateTime date) {
            DateTime monday = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
            int index = (int)Math.Floor((monday - new DateTime(2026, 1, 5)).TotalDays / 7);
            return Quotes[(index % Quotes.Length + Quotes.Length) % Quotes.Length];
        }
    }
}
