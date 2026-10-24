using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Nhip {
    class ScheduleChange {
        public string Id; public DateTime Start; public int Duration;
    }
    class CalendarSegment {
        public TaskItem Task; public int Day, Start, End, Lane, Lanes; public Rectangle Bounds;
    }
    // One painted surface keeps the grid, hit testing and dragging in the same coordinate system.
    class WeekCalendar : Control {
        const int Header = 70, Gutter = 48;
        const float PixelsPerMinute = .8f;
        VScrollBar scroll = new VScrollBar(); ToolTip tip = new ToolTip();
        List<TaskItem> tasks = new List<TaskItem>(); List<CalendarSegment> segments = new List<CalendarSegment>();
        DateTime week;
        TaskItem dragging; DateTime proposed, originalStart; int proposedDuration, originalDuration, mode, grabMinutes;
        bool moved; Point down; string hoverId;
        System.Windows.Forms.Timer edgeTimer = new System.Windows.Forms.Timer();
        ContextMenuStrip menu = new ContextMenuStrip();
        public event Action<ScheduleChange> ScheduleChanged;
        public event Action<TaskItem> EditRequested, ToggleRequested, DeleteRequested, CopyRequested, ColorRequested;
        public event Action<DateTime> CreateRequested;
        public event Action<DateTime> DaySelected;
        public Func<string,string> DeadlineLabel;
        public DateTime SelectedDay { get; set; }
        public void SelectDate(DateTime day) {
            if (day.Date < week || day.Date >= week.AddDays(7)) return;
            SelectedDay = day.Date; Invalidate(); if (DaySelected != null) DaySelected(SelectedDay);
        }
        public int VisibleTaskCount { get { return tasks.Count(t => Start(t) < week.AddDays(7) && Start(t).AddMinutes(t.Duration) > week); } }
        public int ViewMinute { get { return scroll.Value; } set { scroll.Value = Math.Max(0, Math.Min(MaxScroll(), value)); Invalidate(); } }
        public WeekCalendar(DateTime weekStart) {
            week = weekStart.Date; SelectedDay = DateTime.Today; BackColor = Color.White; TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            scroll.Dock = DockStyle.Right; scroll.Minimum = 0; scroll.Maximum = 1439; scroll.SmallChange = 15;
            scroll.ValueChanged += (s, e) => Invalidate(); Controls.Add(scroll);
            Resize += (s, e) => UpdateScroll();
            edgeTimer.Interval = 70; edgeTimer.Tick += (s, e) => {
                if (dragging == null || !moved) return;
                Point p = PointToClient(MousePosition);
                int delta = p.Y < Header + 28 ? -15 : p.Y > Height - 28 ? 15 : 0;
                if (delta != 0) { ViewMinute += delta; Propose(p); }
            };
            UpdateScroll(); ViewMinute = 420;
        }
        public void SetTasks(IEnumerable<TaskItem> items) { tasks = items.ToList(); Invalidate(); }
        int VisibleMinutes() { return Math.Max(30, (int)((Height - Header) / PixelsPerMinute)); }
        int MaxScroll() { return Math.Max(0, 1440 - VisibleMinutes()); }
        void UpdateScroll() { int old = scroll.Value; scroll.LargeChange = Math.Min(1440, VisibleMinutes()); scroll.Value = Math.Min(old, MaxScroll()); }
        float DayWidth { get { return Math.Max(25, (Width - Gutter - scroll.Width) / 7f); } }
        static DateTime Start(TaskItem t) { return DateTime.ParseExact(t.Day + " " + t.Time, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); }
        int Y(int minute) { return Header + (int)Math.Round((minute - ViewMinute) * PixelsPerMinute); }
        int MinuteAt(int y) { return ViewMinute + (int)Math.Round((y - Header) / PixelsPerMinute); }
        static int Snap(int minute) { return (int)Math.Round(minute / 15.0, MidpointRounding.AwayFromZero) * 15; }
        int DayAt(int x) { return Math.Max(0, Math.Min(6, (int)((x - Gutter) / DayWidth))); }
        bool InGrid(Point p) { return p.X >= Gutter && p.X < Width - scroll.Width && p.Y >= Header && p.Y < Height; }
        List<CalendarSegment> LayoutSegments() {
            var result = new List<CalendarSegment>();
            for (int d = 0; d < 7; d++) {
                DateTime date = week.AddDays(d);
                foreach (var t in tasks) {
                    DateTime start = t == dragging && moved ? proposed : Start(t);
                    DateTime end = start.AddMinutes(t == dragging && moved ? proposedDuration : t.Duration);
                    if (start >= date.AddDays(1) || end <= date) continue;
                    result.Add(new CalendarSegment { Task = t, Day = d, Start = (int)Math.Max(0, (start - date).TotalMinutes), End = (int)Math.Min(1440, (end - date).TotalMinutes) });
                }
                var ordered = result.Where(x => x.Day == d).OrderBy(x => x.Start).ThenBy(x => x.End).ToList();
                var group = new List<CalendarSegment>(); var laneEnds = new List<int>(); int endOfGroup = -1;
                foreach (var seg in ordered) {
                    if (seg.Start >= endOfGroup && group.Count > 0) { foreach (var item in group) item.Lanes = laneEnds.Count; group.Clear(); laneEnds.Clear(); }
                    int lane = laneEnds.FindIndex(end => end <= seg.Start);
                    if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(seg.End); } else laneEnds[lane] = seg.End;
                    seg.Lane = lane; group.Add(seg); endOfGroup = group.Max(x => x.End);
                }
                foreach (var item in group) item.Lanes = laneEnds.Count;
            }
            foreach (var seg in result) {
                float laneWidth = (DayWidth - 6) / Math.Max(1, seg.Lanes);
                seg.Bounds = new Rectangle(Gutter + (int)(seg.Day * DayWidth + seg.Lane * laneWidth) + 3, Y(seg.Start), Math.Max(9, (int)laneWidth - 3), Math.Max(18, Y(seg.End) - Y(seg.Start) - 1));
            }
            return result;
        }
        static Rectangle CheckboxBounds(CalendarSegment segment) {
            var r = segment.Bounds;
            return new Rectangle(r.X + 8, r.Height >= 38 ? r.Y + 8 : r.Y + Math.Max(2, (r.Height - 12) / 2), 12, 12);
        }
        static void FillRound(Graphics g, Rectangle bounds, int radius, Color color) {
            if (bounds.Width < 2 || bounds.Height < 2) return;
            radius = Math.Max(1, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            using (var path = Theme.Rounded(bounds, radius)) using (var brush = new SolidBrush(color)) g.FillPath(brush, path);
        }
        void PaintTask(Graphics g, CalendarSegment segment, Font normal, Font small) {
            var r = segment.Bounds; var task = segment.Task; Color color = Theme.TaskColor(task);
            int radius = Math.Min(9, Math.Min(r.Width, r.Height) / 2);
            var shadow = new Rectangle(r.X + 1, r.Y + 2, r.Width, r.Height);
            FillRound(g, shadow, radius, Color.FromArgb(13, 30, 38, 60));
            using (var shape = Theme.Rounded(r, radius)) {
                using (var brush = new SolidBrush(Theme.Tint(color, task.Done ? .07 : .15))) g.FillPath(brush, shape);
                using (var pen = new Pen(Theme.Tint(color, .32))) g.DrawPath(pen, shape);
                var saved = g.Save(); g.SetClip(shape, CombineMode.Intersect);
                using (var brush = new SolidBrush(task.Done ? Theme.Tint(color, .55) : color)) g.FillRectangle(brush, r.X, r.Y, 4, r.Height);
                if (r.Width >= 30) {
                    var box = CheckboxBounds(segment);
                    FillRound(g, box, 3, task.Done ? color : Color.FromArgb(235, 255, 255, 255));
                    using (var path = Theme.Rounded(box, 3)) using (var pen = new Pen(color, 1.2f)) g.DrawPath(pen, path);
                    if (task.Done) using (var pen = new Pen(Color.White, 1.7f)) g.DrawLines(pen, new[] { new Point(box.X + 2, box.Y + 6), new Point(box.X + 5, box.Y + 9), new Point(box.X + 10, box.Y + 3) });
                    if (r.Height < 38 || r.Width < 65) {
                        TextRenderer.DrawText(g, ((DeadlineLabel != null && DeadlineLabel(task.Id) != null) ? "[D] " : "") + task.Title, normal, new Rectangle(r.X + 27, r.Y + 3, Math.Max(1, r.Width - 32), Math.Max(15, r.Height - 5)), task.Done ? Theme.Muted : Theme.Ink, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                    } else {
                        DateTime start = task == dragging && moved ? proposed : Start(task);
                        int duration = task == dragging && moved ? proposedDuration : task.Duration;
                        string times = start.ToString("HH:mm") + "–" + start.AddMinutes(duration).ToString("HH:mm");
                        using (var timeFont = Theme.Font(7.5f, true)) {
                            int available = r.Width - 35;
                            if (TextRenderer.MeasureText(times, timeFont, Size.Empty, TextFormatFlags.NoPadding).Width + 10 > available) times = start.ToString("HH:mm");
                            int pw = Math.Min(available, TextRenderer.MeasureText(times, timeFont, Size.Empty, TextFormatFlags.NoPadding).Width + 10);
                            var pill = new Rectangle(r.X + 27, r.Y + 6, pw, 18); FillRound(g, pill, 8, Color.FromArgb(215, 255, 255, 255));
                            TextRenderer.DrawText(g, times, timeFont, pill, Theme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                        }
                        int nameWidth = Math.Max(1, r.Width - 19), availableHeight = Math.Max(normal.Height, r.Height - 34);
                        int textHeight = Math.Min(normal.Height * 3, availableHeight / normal.Height * normal.Height);
                        var textBox = new Rectangle(r.X + 9, r.Y + (r.Height < 52 ? 25 : 29), nameWidth, textHeight);
                        TextRenderer.DrawText(g, ((DeadlineLabel != null && DeadlineLabel(task.Id) != null) ? "[D] " : "") + task.Title, normal, textBox, task.Done ? Theme.Muted : Theme.Ink, TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                        int measured = TextRenderer.MeasureText(task.Title, normal, new Size(nameWidth, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
                        int tagY = textBox.Top + Math.Min(measured, textHeight) + 5;
                        if (r.Height >= 78 && tagY + 18 <= r.Bottom - 7 && measured <= textHeight) {
                            int tw = Math.Min(nameWidth, TextRenderer.MeasureText(task.Category, small, Size.Empty, TextFormatFlags.NoPadding).Width + 14);
                            var tag = new Rectangle(r.X + 9, tagY, tw, 18); FillRound(g, tag, 8, Theme.Tint(color, .23));
                            TextRenderer.DrawText(g, task.Category, small, tag, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                        }
                    }
                    if (r.Width >= 130 && r.Height >= 38) using (var brush = new SolidBrush(Theme.Tint(color, .7))) {
                        for (int x = 0; x < 2; x++) for (int y = 0; y < 3; y++) g.FillEllipse(brush, r.Right - 12 + x * 4, r.Top + 8 + y * 4, 2, 2);
                    }
                    if (r.Height >= 65) using (var pen = new Pen(Theme.Tint(color, .55), 1.3f)) g.DrawLine(pen, r.X + r.Width / 2 - 5, r.Bottom - 4, r.X + r.Width / 2 + 5, r.Bottom - 4);
                }
                g.Restore(saved);
                if (task == dragging && moved) using (var pen = new Pen(Theme.Purple, 2)) g.DrawPath(pen, shape);
            }
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); var g = e.Graphics; int right = Width - scroll.Width;
            using (var small = Theme.Font(8)) using (var normal = Theme.Font(9.5f, true)) using (var line = new Pen(Color.FromArgb(232, 234, 244))) using (var half = new Pen(Color.FromArgb(239, 240, 248))) {
                g.Clear(Color.White);
                half.DashStyle = DashStyle.Dot;
                using (var fill = new SolidBrush(Color.FromArgb(252, 251, 255))) g.FillRectangle(fill, 0, Header, Gutter, Math.Max(0, Height - Header));
                int today = (DateTime.Today - week).Days;
                int selected = (SelectedDay.Date - week).Days;
                if (selected >= 0 && selected < 7) using (var fill = new SolidBrush(Color.FromArgb(249, 247, 255))) g.FillRectangle(fill, Gutter + selected * DayWidth, Header, DayWidth, Math.Max(0, Height - Header));
                for (int minute = 0; minute <= 1440; minute += 30) {
                    int y = Y(minute); if (y < Header || y > Height) continue;
                    g.DrawLine(minute % 60 == 0 ? line : half, Gutter, y, right, y);
                    if (minute % 60 == 0 && minute < 1440) TextRenderer.DrawText(g, (minute / 60).ToString("00") + ":00", small, new Rectangle(1, Math.Max(Header + 2, y - 7), 44, 18), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.NoPadding);
                }
                for (int d = 0; d <= 7; d++) g.DrawLine(line, Gutter + d * DayWidth, Header, Gutter + d * DayWidth, Height);
                segments = LayoutSegments();
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var state = g.Save(); g.SetClip(new Rectangle(Gutter, Header, Math.Max(1, right - Gutter), Math.Max(1, Height - Header)));
                foreach (var seg in segments) {
                    var r = seg.Bounds; if (r.Bottom < Header || r.Top > Height) continue;
                    PaintTask(g, seg, normal, small);
                }
                if (today >= 0 && today < 7) {
                    int currentY = Y(DateTime.Now.Hour * 60 + DateTime.Now.Minute);
                    if (currentY >= Header && currentY < Height) using (var pen = new Pen(Color.FromArgb(219, 77, 92), 2)) g.DrawLine(pen, Gutter + today * DayWidth, currentY, Gutter + (today + 1) * DayWidth, currentY);
                }
                g.Restore(state);
                using (var fill = new SolidBrush(Color.White)) g.FillRectangle(fill, 0, 0, right, Header);
                TextRenderer.DrawText(g, "Chọn ngày để xem việc · Kéo thả để đổi lịch · Nhấp đúp để thêm", small, new Rectangle(8, 0, right - 12, 19), Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
                for (int d = 0; d < 7; d++) {
                    DateTime date = week.AddDays(d); int x = Gutter + (int)(d * DayWidth), w = (int)DayWidth;
                    FillRound(g, new Rectangle(x + 3, 22, w - 6, Header - 27), 7, date == SelectedDay ? Color.FromArgb(237, 231, 255) : Color.FromArgb(249, 249, 254));
                    string dayName = w >= 77 ? (d == 6 ? "Chủ nhật" : "Thứ " + new[] { "Hai", "Ba", "Tư", "Năm", "Sáu", "Bảy" }[d]) : (d == 6 ? "CN" : "T" + (d + 2));
                    TextRenderer.DrawText(g, dayName, normal, new Rectangle(x, 23, w, 23), date == DateTime.Today || date == SelectedDay ? Theme.Purple : Theme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    var datePill = new Rectangle(x + (w - 44) / 2, 45, 44, 19);
                    if (date == SelectedDay) FillRound(g, datePill, 9, Theme.Purple);
                    TextRenderer.DrawText(g, date.ToString("dd/MM"), small, datePill, date == SelectedDay ? Color.White : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                g.DrawLine(line, 0, Header - 1, right, Header - 1);
                g.DrawRectangle(line, 0, 0, right - 1, Height - 1);
            }
        }
        CalendarSegment Hit(Point p) { if (!InGrid(p)) return null; return LayoutSegments().LastOrDefault(x => x.Bounds.Contains(p)); }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e); Focus();
            if (e.X >= Gutter && e.X < Width - scroll.Width && e.Y >= 21) SelectDate(week.AddDays(DayAt(e.X)));
            var hit = Hit(e.Location);
            if (e.Button == MouseButtons.Right) { if (hit != null) ShowMenu(hit.Task, e.Location); return; }
            if (e.Button != MouseButtons.Left || hit == null) return;
            dragging = hit.Task; down = e.Location; originalStart = Start(dragging); originalDuration = dragging.Duration;
            proposed = originalStart; proposedDuration = originalDuration; moved = false;
            mode = e.Y >= hit.Bounds.Bottom - 7 ? 2 : e.Y <= hit.Bounds.Top + 5 ? 1 : 0;
            grabMinutes = DayAt(e.X) * 1440 + MinuteAt(e.Y) - (int)(originalStart - week).TotalMinutes;
            Capture = true; edgeTimer.Start();
        }
        void Propose(Point p) {
            if (dragging == null) return;
            int absolute = DayAt(p.X) * 1440 + MinuteAt(Math.Max(Header, Math.Min(Height - 1, p.Y)));
            int old = (int)(originalStart - week).TotalMinutes;
            if (mode == 0) { int start = Math.Max(0, Math.Min(7 * 1440 - 15, Snap(absolute - grabMinutes))); proposed = week.AddMinutes(start); proposedDuration = originalDuration; }
            else if (mode == 2) { proposed = originalStart; proposedDuration = Math.Max(15, Math.Min(1440, Snap(absolute) - old)); }
            else { int end = old + originalDuration; int start = Math.Max(Math.Max(0, end - 1440), Math.Min(end - 15, Snap(absolute))); proposed = week.AddMinutes(start); proposedDuration = end - start; }
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (dragging != null) {
                if (!moved && Math.Abs(e.X - down.X) + Math.Abs(e.Y - down.Y) < 5) return;
                moved = true; tip.Hide(this); Cursor = mode == 0 ? Cursors.SizeAll : Cursors.SizeNS; Propose(e.Location); return;
            }
            var hit = Hit(e.Location);
            Cursor = hit == null ? Cursors.Default : Rectangle.Inflate(CheckboxBounds(hit), 2, 2).Contains(e.Location) ? Cursors.Hand : e.Y >= hit.Bounds.Bottom - 7 || e.Y <= hit.Bounds.Top + 5 ? Cursors.SizeNS : Cursors.SizeAll;
            string id = hit == null ? null : hit.Task.Id;
            if (hoverId != id) {
                hoverId = id;
                tip.SetToolTip(this, hit == null ? "Nhấp đúp ô trống để thêm việc." : hit.Task.Title + (DeadlineLabel == null || DeadlineLabel(hit.Task.Id) == null ? "" : "\nDeadline: " + DeadlineLabel(hit.Task.Id)) + "\n" + Start(hit.Task).ToString("dd/MM HH:mm") + "–" + Start(hit.Task).AddMinutes(hit.Task.Duration).ToString("HH:mm") + " · " + hit.Task.Duration + " phút\nTick ô để hoàn thành · Nhấp đúp để sửa · Chuột phải để xem tùy chọn");
            }
        }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e); if (dragging == null || e.Button != MouseButtons.Left) return;
            var task = dragging; bool commit = moved && InGrid(e.Location); bool toggle = !moved && Math.Abs(e.X - down.X) + Math.Abs(e.Y - down.Y) < 5;
            var segment = Hit(e.Location); bool inBox = segment != null && segment.Task.Id == task.Id && Rectangle.Inflate(CheckboxBounds(segment), 2, 2).Contains(e.Location);
            var change = new ScheduleChange { Id = task.Id, Start = proposed, Duration = proposedDuration };
            dragging = null; moved = false; edgeTimer.Stop(); Capture = false; Cursor = Cursors.Default; Invalidate();
            if (commit && (change.Start != originalStart || change.Duration != originalDuration) && ScheduleChanged != null) ScheduleChanged(change);
            else if (toggle && inBox && ToggleRequested != null) ToggleRequested(task);
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e) {
            base.OnMouseDoubleClick(e); CancelDrag(); if (!InGrid(e.Location)) return;
            var hit = Hit(e.Location);
            if (hit != null) { if (EditRequested != null) EditRequested(hit.Task); }
            else if (CreateRequested != null) CreateRequested(week.AddDays(DayAt(e.X)).AddMinutes(Math.Max(0, Math.Min(1425, Snap(MinuteAt(e.Y))))));
        }
        void ShowMenu(TaskItem t, Point p) {
            menu.Items.Clear(); menu.Items.Add("Chỉnh sửa", null, (s, e) => { if (EditRequested != null) EditRequested(t); });
            menu.Items.Add("Sao chép…", null, (s, e) => { if (CopyRequested != null) CopyRequested(t); });
            menu.Items.Add("Đổi màu…", null, (s, e) => { if (ColorRequested != null) ColorRequested(t); });
            menu.Items.Add(t.Done ? "Bỏ hoàn thành" : "Đánh dấu hoàn thành", null, (s, e) => { if (ToggleRequested != null) ToggleRequested(t); });
            menu.Items.Add("Xóa công việc", null, (s, e) => { if (DeleteRequested != null) DeleteRequested(t); }); menu.Show(this, p);
        }
        void CancelDrag() { dragging = null; moved = false; Capture = false; edgeTimer.Stop(); Cursor = Cursors.Default; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture && dragging != null) CancelDrag(); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { if (keyData == Keys.Escape && dragging != null) { CancelDrag(); return true; } return base.ProcessCmdKey(ref msg, keyData); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); if (dragging == null) ViewMinute -= Math.Sign(e.Delta) * 45; }
        protected override void Dispose(bool disposing) { if (disposing) { edgeTimer.Dispose(); tip.Dispose(); UiMenus.DisposeAfterEvent(menu); } base.Dispose(disposing); }
        public bool SimulateGesture(string id, int days, int minutes, int resizeMode, bool cancel) {
            var task = tasks.First(t => t.Id == id); int taskMinute = (int)TimeSpan.Parse(task.Time).TotalMinutes;
            ViewMinute = Math.Max(0, taskMinute - 60); var seg = LayoutSegments().First(x => x.Task.Id == id && x.Start == taskMinute);
            var p = new Point(seg.Bounds.X + Math.Min(32, seg.Bounds.Width - 5), resizeMode == 2 ? seg.Bounds.Bottom - 3 : resizeMode == 1 ? seg.Bounds.Top + 2 : seg.Bounds.Top + 24);
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0));
            Point target = new Point(p.X + (int)Math.Round(days * DayWidth), p.Y + (int)Math.Round(minutes * PixelsPerMinute));
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, target.X, target.Y, 0));
            if (cancel) CancelDrag(); else OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, target.X, target.Y, 0));
            return dragging == null;
        }
        public bool OverlapHasSeparateLanes() {
            var all = LayoutSegments();
            return !all.Any(a => all.Any(b => a != b && a.Day == b.Day && a.Start < b.End && b.Start < a.End && a.Bounds.Left < b.Bounds.Right && b.Bounds.Left < a.Bounds.Right));
        }
        public void ToggleForTest(string id) {
            var task = tasks.First(t => t.Id == id); ViewMinute = Math.Max(0, (int)TimeSpan.Parse(task.Time).TotalMinutes - 60);
            var segment = LayoutSegments().First(s => s.Task.Id == id); var box = CheckboxBounds(segment); var point = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)); OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
        }
        public void InvokeMenuForTest(string id, string command) {
            ShowMenu(tasks.First(t => t.Id == id), new Point(80, 100));
            var item = menu.Items.OfType<ToolStripMenuItem>().First(x => x.Text == command); menu.Close(); item.PerformClick();
        }
    }
}
