using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Nhip {
    public class Deadline {
        public string Id { get; set; }
        public string Title { get; set; }
        public string DueDay { get; set; }
        public string DueTime { get; set; }
        public string Notes { get; set; }
        public string CompletedAt { get; set; }
        public List<string> TaskIds { get; set; }
        public Deadline() { TaskIds = new List<string>(); }
        public DateTime Due { get { return DateTime.ParseExact(DueDay + " " + (String.IsNullOrEmpty(DueTime) ? "23:59" : DueTime), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).AddTicks(String.IsNullOrEmpty(DueTime) ? TimeSpan.TicksPerMinute - 1 : 0); } }
        public bool Completed { get { return !String.IsNullOrEmpty(CompletedAt); } }
        public string Status(DateTime now) { return Completed ? "Đã hoàn tất" : Due < now ? "Quá hạn" : Due <= now.AddDays(3) ? "Sắp đến hạn" : "Đang làm"; }
    }
    public partial class Store {
        public IEnumerable<TaskItem> AllTasks { get { return Data.Tasks.Concat(Data.UnscheduledTasks); } }
        public IEnumerable<TaskItem> DeadlineTasks(Deadline d) { return d.TaskIds.Select(id => AllTasks.First(t => t.Id == id)); }
        public Deadline ParentDeadline(string taskId) { return Data.Deadlines.FirstOrDefault(d => d.TaskIds.Contains(taskId)); }
        public bool IsScheduled(string id) { return Data.Tasks.Any(t => t.Id == id); }
        public IEnumerable<TaskItem> DeadlineSchedulingTasks(string deadlineId,bool onlyUnscheduled) {
            var parent=Data.Deadlines.FirstOrDefault(d=>d.Id==deadlineId);
            if(parent==null || parent.Completed)return Enumerable.Empty<TaskItem>();
            return DeadlineTasks(parent).Where(t=>!t.Done && (!onlyUnscheduled || !IsScheduled(t.Id))).OrderBy(t=>IsScheduled(t.Id)).ThenBy(t=>t.Title).ToList();
        }
        public void ScheduleTaskFromDeadline(string deadlineId,string taskId,string day,string time,int duration,bool allowReschedule) {
            Commit(()=>{
                var parent=Data.Deadlines.FirstOrDefault(d=>d.Id==deadlineId);
                var task=AllTasks.FirstOrDefault(t=>t.Id==taskId);
                if(parent==null || parent.Completed || task==null || !parent.TaskIds.Contains(taskId) || task.Done)throw new InvalidOperationException("Công việc hoặc deadline đã thay đổi. Hãy chọn lại việc chưa hoàn thành.");
                if(IsScheduled(taskId) && !allowReschedule)throw new InvalidOperationException("Công việc đã có lịch. Cần xác nhận đổi lịch trước khi lưu.");
                var updated=new TaskItem {Id=task.Id,Title=task.Title,Day=day,Time=time,Duration=duration,Category=task.Category,Reward=task.Reward,ColorHex=task.ColorHex,Done=task.Done,Awarded=task.Awarded};
                Validate(updated);Data.Tasks.Remove(task);Data.UnscheduledTasks.Remove(task);Data.Tasks.Add(updated);
            });
        }
        public void SaveTaskWithDeadline(TaskItem item, string deadlineId) {
            Validate(item);
            Commit(() => {
                var old = AllTasks.FirstOrDefault(t => t.Id == item.Id);
                var parent = ParentDeadline(item.Id);
                var target = String.IsNullOrEmpty(deadlineId) ? null : Data.Deadlines.FirstOrDefault(d => d.Id == deadlineId);
                if (!String.IsNullOrEmpty(deadlineId) && target == null) throw new InvalidOperationException("Deadline không còn tồn tại. Hãy chọn lại.");
                if (target != null && target.Completed && target != parent) throw new InvalidOperationException("Mở lại deadline trước khi thêm công việc.");
                item.Done = old != null && old.Done; item.Awarded = old == null ? 0 : old.Awarded;
                if (old != null) { Data.Tasks.Remove(old); Data.UnscheduledTasks.Remove(old); }
                Data.Tasks.Add(item);
                if (parent != target) {
                    if (parent != null) { parent.TaskIds.Remove(item.Id); if (parent.TaskIds.Count == 0) parent.CompletedAt = null; }
                    if (target != null) target.TaskIds.Add(item.Id);
                }
            });
        }
        static void ValidateDeadline(Deadline d) {
            DateTime day, stamp; TimeSpan time;
            if (d == null || String.IsNullOrWhiteSpace(d.Id) || String.IsNullOrWhiteSpace(d.Title) || d.Title.Length > 160 || d.TaskIds == null || d.TaskIds.Distinct().Count() != d.TaskIds.Count || (d.Notes != null && d.Notes.Length > 4000) || !DateTime.TryParseExact(d.DueDay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day) || (!String.IsNullOrEmpty(d.DueTime) && !TimeSpan.TryParseExact(d.DueTime, "hh\\:mm", CultureInfo.InvariantCulture, out time)) || (!String.IsNullOrEmpty(d.CompletedAt) && !DateTime.TryParse(d.CompletedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out stamp))) throw new InvalidDataException("Deadline cần tên, ngày hạn và giờ hợp lệ.");
        }
        static void ValidateDeadlines(AppData d) {
            if (d.Deadlines == null) d.Deadlines = new List<Deadline>();
            if (d.UnscheduledTasks == null) d.UnscheduledTasks = new List<TaskItem>();
            foreach (var t in d.UnscheduledTasks) Validate(t);
            var tasks = d.Tasks.Concat(d.UnscheduledTasks).ToList();
            if (tasks.Select(t => t.Id).Distinct().Count() != tasks.Count || d.Deadlines.Select(x => x.Id).Distinct().Count() != d.Deadlines.Count) throw new InvalidDataException("Mã công việc hoặc deadline bị trùng.");
            var links = new HashSet<string>();
            foreach (var deadline in d.Deadlines) {
                ValidateDeadline(deadline);
                foreach (var id in deadline.TaskIds) if (!tasks.Any(t => t.Id == id) || !links.Add(id)) throw new InvalidDataException("Liên kết deadline không hợp lệ.");
                if (deadline.Completed && (deadline.TaskIds.Count == 0 || tasks.Any(t => deadline.TaskIds.Contains(t.Id) && !t.Done))) throw new InvalidDataException("Deadline hoàn tất còn việc chưa xong.");
            }
        }
        public void SaveDeadline(Deadline draft) {
            ValidateDeadline(draft);
            Commit(() => {
                var old = Data.Deadlines.FirstOrDefault(d => d.Id == draft.Id);
                if (old == null) Data.Deadlines.Add(new Deadline { Id = draft.Id, Title = draft.Title.Trim(), DueDay = draft.DueDay, DueTime = draft.DueTime, Notes = draft.Notes });
                else { old.Title = draft.Title.Trim(); old.DueDay = draft.DueDay; old.DueTime = draft.DueTime; old.Notes = draft.Notes; }
            });
        }
        public void AttachTask(string deadlineId, string taskId) {
            Commit(() => {
                var d = Data.Deadlines.First(x => x.Id == deadlineId);
                if (d.Completed) throw new InvalidOperationException("Mở lại deadline trước khi thêm việc.");
                if (!AllTasks.Any(t => t.Id == taskId)) throw new InvalidOperationException("Không tìm thấy công việc.");
                if (ParentDeadline(taskId) != null) throw new InvalidOperationException("Công việc đã thuộc một deadline.");
                d.TaskIds.Add(taskId);
            });
        }
        public void AddDeadlineTask(string deadlineId, TaskItem draft) {
            Validate(draft);
            Commit(() => {
                var d = Data.Deadlines.First(x => x.Id == deadlineId);
                if (d.Completed || AllTasks.Any(t => t.Id == draft.Id)) throw new InvalidOperationException("Không thể thêm công việc này.");
                draft.Done = false; draft.Awarded = 0; Data.UnscheduledTasks.Add(draft); d.TaskIds.Add(draft.Id);
            });
        }
        public void DetachTask(string deadlineId, string taskId) {
            Commit(() => { var d = Data.Deadlines.First(x => x.Id == deadlineId); d.TaskIds.Remove(taskId); if (d.TaskIds.Count == 0) d.CompletedAt = null; });
        }
        public void DeleteDeadline(string id) { Commit(() => Data.Deadlines.RemoveAll(d => d.Id == id)); }
        public void CompleteDeadline(string id, bool complete) {
            Commit(() => {
                var d = Data.Deadlines.First(x => x.Id == id);
                if (complete && (d.TaskIds.Count == 0 || DeadlineTasks(d).Any(t => !t.Done))) throw new InvalidOperationException("Hoàn thành tất cả việc nhỏ trước khi xác nhận bàn giao.");
                if (complete && !d.Completed) d.CompletedAt = Clock().ToString("o");
                if (!complete) d.CompletedAt = null;
            });
        }
        public void ScheduleDeadlineTask(TaskItem draft) {
            Validate(draft);
            Commit(() => {
                var old = AllTasks.First(t => t.Id == draft.Id);
                draft.Done = old.Done; draft.Awarded = old.Awarded;
                Data.Tasks.Remove(old); Data.UnscheduledTasks.Remove(old); Data.Tasks.Add(draft);
            });
        }
        public void SavePendingTask(TaskItem draft) {
            Validate(draft);
            Commit(() => {
                var old = Data.UnscheduledTasks.First(t => t.Id == draft.Id);
                old.Title = draft.Title; old.Duration = draft.Duration; old.Category = draft.Category; old.Reward = draft.Reward;
            });
        }
    }
}
