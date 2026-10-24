using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Nhip {
    sealed class StatisticsMetric : Card {
        public string Caption = "", Value = "", Detail = "";
        public Color ValueColor = Theme.Ink;
        public double? Fraction;
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            using (var small = Theme.Font(8)) using (var large = Theme.Font(Width >= 190 ? 17 : 14, true)) {
                TextRenderer.DrawText(e.Graphics, Caption, small, new Rectangle(13, 10, Width - 26, 22), Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(e.Graphics, Value, large, new Rectangle(13, 35, Width - 26, 32), ValueColor, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(e.Graphics, Detail, small, new Rectangle(13, 73, Width - 26, 20), Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }
            if (Fraction.HasValue) {
                var r = new Rectangle(14, Height - 12, Width - 28, 4);
                using (var brush = new SolidBrush(Theme.Line)) e.Graphics.FillRectangle(brush, r);
                r.Width = (int)(r.Width * Math.Max(0, Math.Min(1, Fraction.Value)));
                using (var brush = new SolidBrush(Theme.Purple)) e.Graphics.FillRectangle(brush, r);
            }
            AccessibleName = Caption + ": " + Value + ". " + Detail;
        }
    }
    sealed class StatisticsBars : Control {
        public List<StatsBucket> Buckets = new List<StatsBucket>();
        public string Grouping = "Ngày";
        public static readonly Color WorkColor = Color.FromArgb(20, 158, 161);
        readonly ToolTip tip = new ToolTip();
        readonly List<KeyValuePair<Rectangle, StatsBucket>> hitAreas = new List<KeyValuePair<Rectangle, StatsBucket>>();
        public StatisticsBars() { DoubleBuffered = true; BackColor = Color.White; }
        public int DisplayedCount { get { return Math.Min(Buckets.Count, Grouping == "Ngày" ? 31 : 12); } }
        static double Step(double value) {
            if (value <= 0) return 1;
            double power = Math.Pow(10, Math.Floor(Math.Log10(value)));
            foreach (double n in new[] { 1.0, 2, 2.5, 3, 5, 10 }) if (n * power >= value) return n * power;
            return power * 10;
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; hitAreas.Clear();
            var data = Buckets.Skip(Math.Max(0, Buckets.Count - DisplayedCount)).ToList();
            var plot = new Rectangle(48, 54, Math.Max(30, Width - 66), Math.Max(40, Height - 119));
            using (var small = Theme.Font(8)) using (var title = Theme.Font(10, true)) using (var line = new Pen(Theme.Line)) {
                string caption = Buckets.Count > data.Count ? "Thời gian tập trung · " + data.Count + " kỳ gần nhất" : "Thời gian tập trung · " + data.Count + " " + (Grouping == "Ngày" ? "ngày" : Grouping == "Tuần" ? "tuần" : "tháng");
                TextRenderer.DrawText(g, caption, title, new Rectangle(14, 10, Width - 28, 27), Theme.Ink, TextFormatFlags.EndEllipsis);
                double maximum = data.Count == 0 ? 0 : data.Max(b => b.FocusSeconds / 60);
                double step = maximum <= 0 ? 12 : Step(maximum / 5), upper = Math.Max(step, Math.Ceiling(maximum / step) * step);
                if (maximum <= 0) upper = 60;
                TextRenderer.DrawText(g, "Phút", small, new Rectangle(8, 34, 42, 18), Theme.Muted);
                for (double n = 0; n <= upper + step / 2; n += step) {
                    int y = plot.Bottom - (int)(plot.Height * n / upper); g.DrawLine(line, plot.Left, y, plot.Right, y);
                    TextRenderer.DrawText(g, n.ToString("0.##"), small, new Rectangle(0, y - 8, 42, 18), Theme.Muted, TextFormatFlags.Right);
                }
                float slot = plot.Width / (float)Math.Max(1, data.Count);
                int labelEvery = Math.Max(1, (int)Math.Ceiling(48 / slot));
                for (int i = 0; i < data.Count; i++) {
                    var b = data[i]; int x = plot.Left + (int)(slot * i), bw = Math.Max(3, Math.Min(44, (int)slot - 10));
                    int cx = x + (int)slot / 2, study = (int)(plot.Height * b.StudySeconds / 60 / upper), work = (int)(plot.Height * b.WorkSeconds / 60 / upper);
                    if (b.StudySeconds > 0) study = Math.Max(1, study); if (b.WorkSeconds > 0) work = Math.Max(1, work);
                    using (var brush = new SolidBrush(Theme.Purple)) g.FillRectangle(brush, cx - bw / 2, plot.Bottom - study, bw, study);
                    using (var brush = new SolidBrush(WorkColor)) g.FillRectangle(brush, cx - bw / 2, plot.Bottom - study - work, bw, work);
                    if (b.FocusSeconds <= 0) using (var brush = new SolidBrush(Color.FromArgb(208, 216, 236))) g.FillEllipse(brush, cx - 3, plot.Bottom - 3, 6, 6);
                    if (b.FocusSeconds > 0 && slot >= 50) TextRenderer.DrawText(g, (b.FocusSeconds / 60).ToString("0.#"), small, new Rectangle(cx - 30, plot.Bottom - study - work - 21, 60, 20), Theme.Ink, TextFormatFlags.HorizontalCenter);
                    if (i % labelEvery == 0 || i == data.Count - 1) TextRenderer.DrawText(g, b.From.ToString(Grouping == "Tháng" ? "MM/yy" : "dd/MM"), small, new Rectangle(cx - 26, plot.Bottom + 8, 52, 20), Theme.Muted, TextFormatFlags.HorizontalCenter);
                    hitAreas.Add(new KeyValuePair<Rectangle, StatsBucket>(new Rectangle(x, plot.Top, Math.Max(1, (int)slot), plot.Height + 30), b));
                }
                if (maximum <= 0) TextRenderer.DrawText(g, "Chưa ghi nhận thời gian bấm giờ", small, new Rectangle(plot.Left + 10, plot.Top + 30, plot.Width - 20, 45), Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
                int ly = Height - 24, lx = Math.Max(16, Width / 2 - 107);
                using (var brush = new SolidBrush(Theme.Purple)) g.FillEllipse(brush, lx, ly + 3, 9, 9);
                TextRenderer.DrawText(g, "Học tập", small, new Rectangle(lx + 15, ly, 80, 22), Theme.Muted);
                using (var brush = new SolidBrush(WorkColor)) g.FillEllipse(brush, lx + 105, ly + 3, 9, 9);
                TextRenderer.DrawText(g, "Công việc", small, new Rectangle(lx + 120, ly, 90, 22), Theme.Muted);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e); var hit = hitAreas.FirstOrDefault(h => h.Key.Contains(e.Location)); var b = hit.Value;
            string text = b == null ? "" : b.From.ToString("dd/MM/yyyy") + " – " + b.To.AddDays(-1).ToString("dd/MM/yyyy") + "\nHọc tập: " + FocusPage.Duration(b.StudySeconds) + "\nCông việc: " + FocusPage.Duration(b.WorkSeconds);
            if (tip.GetToolTip(this) != text) tip.SetToolTip(this, text);
        }
        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
    }
    sealed class StatisticsDonut : Control {
        public double Study, Work;
        public StatisticsDonut() { DoubleBuffered = true; BackColor = Color.White; }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            double total = Study + Work; int d = Math.Max(60, Math.Min(158, Math.Min(Width - 40, Height - 146)));
            var r = new Rectangle((Width - d) / 2, 54, d, d);
            using (var small = Theme.Font(8)) using (var heading = Theme.Font(10, true)) using (var digits = Theme.Font(15, true)) {
                TextRenderer.DrawText(g, "Phân bổ thời gian", heading, new Rectangle(12, 10, Width - 24, 28), Theme.Ink, TextFormatFlags.HorizontalCenter);
                using (var pen = new Pen(Theme.Line, 19)) g.DrawEllipse(pen, r);
                if (total > 0) {
                    float angle = (float)(360 * Study / total);
                    if (Study > 0) using (var pen = new Pen(Theme.Purple, 19)) g.DrawArc(pen, r, -90, Math.Min(360, angle));
                    if (Work > 0) using (var pen = new Pen(StatisticsBars.WorkColor, 19)) g.DrawArc(pen, r, -90 + angle, Math.Min(360, 360 - angle));
                }
                TextRenderer.DrawText(g, StatisticsPage.TimeText(total), digits, new Rectangle(r.X + 15, r.Y + d / 2 - 27, d - 30, 58), Theme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                int y = r.Bottom + 30;
                int studyPercent = total > 0 ? (int)Math.Round(100 * Study / total, MidpointRounding.AwayFromZero) : 0;
                DrawLegend(g, small, "Học tập", Study, total, studyPercent, Theme.Purple, y); DrawLegend(g, small, "Công việc", Work, total, total > 0 ? 100 - studyPercent : 0, StatisticsBars.WorkColor, y + 28);
            }
        }
        void DrawLegend(Graphics g, Font font, string name, double value, double total, int percent, Color color, int y) {
            using (var brush = new SolidBrush(color)) g.FillEllipse(brush, 14, y + 4, 9, 9);
            TextRenderer.DrawText(g, name, font, new Rectangle(30, y, 80, 24), Theme.Ink);
            TextRenderer.DrawText(g, StatisticsPage.TimeText(value) + (total > 0 ? " · " + percent + "%" : ""), font, new Rectangle(102, y, Math.Max(20, Width - 112), 24), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
        }
    }
    class StatisticsPage : UserControl {
        readonly Store store;
        DailyStatisticsPage daily;
        readonly Panel scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        readonly Panel content = new Panel();
        readonly DateTimePicker from = new DateTimePicker(), to = new DateTimePicker();
        readonly Button grouping = Theme.Button("Theo ngày ⌄");
        readonly List<Button> presets = new List<Button>();
        readonly Card filter = new Card(), chartCard = new Card(), donutCard = new Card(), tableCard = new Card();
        readonly StatisticsMetric focus = new StatisticsMetric(), tasks = new StatisticsMetric(), leisure = new StatisticsMetric(), goals = new StatisticsMetric();
        readonly StatisticsBars bars = new StatisticsBars(); readonly StatisticsDonut donut = new StatisticsDonut();
        readonly Label heading, note, range, detailHeading, footer, empty, dateSeparator;
        readonly CheckBox onlyActivity = new CheckBox(), moreDetails = new CheckBox();
        readonly DataGridView grid = Grids.Create();
        readonly Button apply, more;
        List<StatsBucket> values = new List<StatsBucket>();
        string selectedPreset = "7 ngày", selectedGrouping = "Ngày"; bool layingOut;
        public StatisticsPage(Store source) {
            store = source; Dock = DockStyle.Fill; BackColor = Theme.Bg; scrollHost.BackColor = content.BackColor = Theme.Bg; Controls.Add(scrollHost); scrollHost.Controls.Add(content);
            heading = Theme.Label("Nhìn lại hành trình", 25, Theme.Ink, true); content.Controls.Add(heading); content.Controls.Add(filter);
            foreach (string text in new[] { "Một ngày", "7 ngày", "30 ngày", "12 tháng", "Tùy chọn" }) {
                var button = Theme.Button(text); button.Font = Theme.Font(9, true); button.Tag = text; button.Click += (s, e) => SetPreset((string)button.Tag); filter.Controls.Add(button); presets.Add(button);
            }
            more = Theme.Button("⋯"); filter.Controls.Add(more); var menu = new ContextMenuStrip();
            menu.Items.Add("90 ngày", null, (s, e) => SetPreset("90 ngày")); menu.Items.Add("Tất cả", null, (s, e) => SetPreset("Tất cả"));
            more.Click += (s, e) => menu.Show(more, new Point(0, more.Height)); more.Disposed += (s, e) => menu.Dispose();
            grouping.Font = Theme.Font(9); filter.Controls.Add(grouping); grouping.AccessibleName = "Nhóm thống kê theo ngày, tuần hoặc tháng";
            var groupMenu = new ContextMenuStrip();
            foreach (string label in new[] { "Ngày", "Tuần", "Tháng" }) { string groupName = label; groupMenu.Items.Add(label, null, (s, e) => SetGrouping(groupName)); }
            grouping.Click += (s, e) => groupMenu.Show(grouping, new Point(0, grouping.Height)); grouping.Disposed += (s, e) => groupMenu.Dispose();
            apply = Theme.Button("Áp dụng", true); apply.Font = Theme.Font(9, true); apply.Click += (s, e) => RefreshData(); filter.Controls.Add(apply);
            range = Theme.Label("", 9, Theme.Muted); range.AutoEllipsis = true; filter.Controls.Add(range);
            dateSeparator = Theme.Label("→", 10, Theme.Muted); filter.Controls.Add(dateSeparator);
            foreach (var picker in new[] { from, to }) { picker.Format = DateTimePickerFormat.Custom; picker.CustomFormat = "dd/MM/yyyy"; picker.Font = Theme.Font(10); picker.AccessibleName = picker == from ? "Từ ngày" : "Đến ngày"; filter.Controls.Add(picker); }
            note = Theme.Label("Việc xong theo trạng thái hiện tại · Giờ tập trung từ bấm giờ", 8.5f, Theme.Muted); note.AutoEllipsis = true; content.Controls.Add(note);
            foreach (var metric in new[] { focus, tasks, leisure, goals }) content.Controls.Add(metric);
            focus.Caption = "THỜI GIAN TẬP TRUNG"; focus.ValueColor = Theme.Purple; tasks.Caption = "VIỆC HOÀN THÀNH"; leisure.Caption = "GIẢI TRÍ ĐÃ DÙNG"; goals.Caption = "MỤC TIÊU THÁNG"; tasks.ValueColor=Theme.Green;leisure.ValueColor=Color.FromArgb(185,120,35);goals.ValueColor=Color.FromArgb(40,112,191);
            bars.Dock = donut.Dock = DockStyle.Fill; chartCard.Padding = donutCard.Padding = new Padding(1); chartCard.Controls.Add(bars); donutCard.Controls.Add(donut); content.Controls.Add(chartCard); content.Controls.Add(donutCard);
            detailHeading = Theme.Label("CHI TIẾT THEO NGÀY", 9, Theme.Ink, true); content.Controls.Add(detailHeading);
            onlyActivity.Text = "Ẩn kỳ trống"; moreDetails.Text = "Thêm chỉ số";
            foreach (var check in new[] { onlyActivity, moreDetails }) { check.Font = Theme.Font(8.5f); check.ForeColor = Theme.Muted; check.BackColor = Theme.Bg; content.Controls.Add(check); }
            onlyActivity.Checked = true; onlyActivity.CheckedChanged += (s, e) => RenderRows(); moreDetails.CheckedChanged += (s, e) => SetDetailColumns();
            grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = grid.AllowUserToDeleteRows = false; grid.RowTemplate.Height = 44; grid.Font = Theme.Font(9); grid.ColumnHeadersHeight = 34; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 249, 255); grid.DefaultCellStyle.Padding = new Padding(5);
            string[] ids = { "period", "study", "work", "total", "done", "earned", "revoked", "leisure", "goals" };
            string[] captions = { "Kỳ", "Học tập", "Công việc", "Tổng tập trung", "Việc xong", "Thưởng (p)", "Thu hồi (p)", "Giải trí (p)", "Mục tiêu" };
            for (int i = 0; i < ids.Length; i++) { Grids.Column(grid, ids[i], captions[i], i == 0 ? 25 : 22, true); grid.Columns[ids[i]].MinimumWidth = i == 0 ? 84 : 74; }
            grid.Columns["total"].DefaultCellStyle.ForeColor = Theme.Purple;
            foreach(DataGridViewColumn c in grid.Columns)if(c.Name!="period"){c.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;c.HeaderCell.Style.Alignment=DataGridViewContentAlignment.MiddleRight;}
            grid.Columns["earned"].DefaultCellStyle.ForeColor=Theme.Green;grid.Columns["revoked"].DefaultCellStyle.ForeColor=Color.FromArgb(190,65,83);
            tableCard.Padding = new Padding(1); tableCard.Controls.Add(grid); content.Controls.Add(tableCard);
            empty = Theme.Label("Chưa có hoạt động trong khoảng đã chọn.\nChọn khoảng khác hoặc bỏ ẩn kỳ trống.", 10, Theme.Muted); empty.Dock = DockStyle.Fill; empty.TextAlign = ContentAlignment.MiddleCenter; tableCard.Controls.Add(empty);
            footer = Theme.Label("", 8, Theme.Muted); content.Controls.Add(footer);
            scrollHost.Resize += (s, e) => Arrange(); VisibleChanged += (s, e) => { if (Visible) Arrange(); };
            daily=new DailyStatisticsPage(store);daily.Visible=false;daily.RangeRequested+=preset=>SetPreset(preset);Controls.Add(daily);
            SetDetailColumns(); SetPreset("7 ngày");
        }
        public static string TimeText(double seconds) {
            if (seconds <= 0) return "0 phút"; if (seconds < 60) return "<1 phút";
            long minutes = (long)Math.Floor(seconds / 60); return minutes >= 60 ? (minutes / 60) + " giờ" + (minutes % 60 > 0 ? " " + (minutes % 60) + " phút" : "") : minutes + " phút";
        }
        void SetPreset(string preset) {
            if(preset=="Một ngày"){selectedPreset=preset;scrollHost.Visible=false;daily.Visible=true;daily.BringToFront();daily.RefreshData();return;}
            if(daily!=null)daily.Visible=false;scrollHost.Visible=true;scrollHost.BringToFront();
            selectedPreset = preset; DateTime end = DateTime.Today, begin = end.AddDays(-6);
            if (preset == "30 ngày") begin = end.AddDays(-29);
            if (preset == "90 ngày") begin = end.AddDays(-89);
            if (preset == "12 tháng") begin = new DateTime(end.Year, end.Month, 1).AddMonths(-11);
            if (preset == "Tất cả") AllRange(out begin, out end);
            if (preset != "Tùy chọn") { from.Value = begin; to.Value = end; selectedGrouping = preset == "12 tháng" || preset == "Tất cả" ? "Tháng" : preset == "90 ngày" ? "Tuần" : "Ngày"; }
            from.Visible = to.Visible = preset == "Tùy chọn";
            dateSeparator.Visible = preset == "Tùy chọn";
            foreach (var button in presets) { bool selected = (string)button.Tag == preset; button.BackColor = selected ? Theme.Purple : Color.White; button.ForeColor = selected ? Color.White : Theme.Ink; }
            more.BackColor = preset == "90 ngày" || preset == "Tất cả" ? Theme.Sidebar : Color.White;
            grouping.Text = "Theo " + selectedGrouping.ToLowerInvariant() + " ⌄"; if (preset != "Tùy chọn") RefreshData(); else Arrange();
        }
        void AllRange(out DateTime begin, out DateTime end) {
            var days = store.Data.Tasks.Select(t => DateTime.ParseExact(t.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList();
            foreach (var session in store.Data.FocusSessions) { days.Add(new DateTime(session.Created, DateTimeKind.Utc).ToLocalTime().Date); foreach (var slice in session.Slices) { days.Add(new DateTime(slice.Start, DateTimeKind.Utc).ToLocalTime().Date); days.Add(new DateTime(Math.Max(slice.Start, slice.End - 1), DateTimeKind.Utc).ToLocalTime().Date); } }
            foreach (var entry in store.Data.History) { DateTime at; if (DateTime.TryParse(entry.At, null, DateTimeStyles.RoundtripKind, out at)) { days.Add(at.ToLocalTime().Date); if (entry.Detail == "Giải trí" && entry.Seconds < 0) days.Add(at.ToLocalTime().AddSeconds(entry.Seconds).Date); } }
            foreach (var goal in store.Data.Goals) days.Add(DateTime.ParseExact(goal.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (store.Data.ActiveFocus != null) { days.Add(new DateTime(store.Data.ActiveFocus.Created, DateTimeKind.Utc).ToLocalTime().Date); days.Add(store.Clock().ToLocalTime().Date); }
            begin = days.Count > 0 ? days.Min() : DateTime.Today; end = days.Count > 0 ? new[] { DateTime.Today, days.Max() }.Max() : DateTime.Today;
        }
        public void RefreshData() {
            if(selectedPreset=="Một ngày"){daily.RefreshData();return;}
            try {
                if (to.Value.Date < from.Value.Date || to.Value.Date.Year - from.Value.Date.Year > 100) throw new InvalidOperationException("Chọn khoảng ngày hợp lệ, tối đa 100 năm.");
                var next = store.Statistics(from.Value.Date, to.Value.Date.AddDays(1), selectedGrouping); values = next;
                int planned = values.Sum(b => b.Planned), completed = values.Sum(b => b.Completed), goalCount = values.Sum(b => b.Goals), goalDone = values.Sum(b => b.GoalsDone);
                double study = values.Sum(b => b.StudySeconds), work = values.Sum(b => b.WorkSeconds);
                range.Text = selectedPreset + "  ·  " + from.Value.ToString("dd/MM/yyyy") + " — " + to.Value.ToString("dd/MM/yyyy");
                focus.Value = TimeText(study + work); focus.Detail = FocusPage.Duration(study + work) + " thời gian thực";
                tasks.Value = completed + " / " + planned; tasks.Fraction = planned > 0 ? (double?)completed / planned : null; tasks.Detail = planned > 0 ? (100.0 * completed / planned).ToString("0") + "% đã hoàn thành" : "Chưa có công việc";
                leisure.Value = TimeText(values.Sum(b => b.EntertainmentMinutes) * 60); leisure.Detail = "Trong khoảng đã chọn";
                goals.Caption = from.Value.ToString("yyyy-MM") == to.Value.ToString("yyyy-MM") ? "MỤC TIÊU THÁNG " + to.Value.ToString("MM/yyyy") : "MỤC TIÊU TRONG KỲ";
                goals.Value = goalDone + " / " + goalCount; goals.Fraction = goalCount > 0 ? (double?)goalDone / goalCount : null; goals.Detail = goalCount > 0 ? (100.0 * goalDone / goalCount).ToString("0") + "% · Trạng thái hiện tại" : "Chưa có mục tiêu";
                foreach (var metric in new[] { focus, tasks, leisure, goals }) metric.Invalidate();
                bars.Buckets = values; bars.Grouping = selectedGrouping; bars.Invalidate(); donut.Study = study; donut.Work = work; donut.Invalidate();
                detailHeading.Text = "CHI TIẾT THEO " + selectedGrouping.ToUpperInvariant(); RenderRows(); Arrange();
            } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Thống kê", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        static bool HasActivity(StatsBucket b) { return b.FocusSeconds > 0 || b.Planned > 0 || b.EarnedMinutes > 0 || b.RevokedMinutes > 0 || b.EntertainmentMinutes > 0 || b.Goals > 0; }
        void RenderRows() {
            grid.Rows.Clear();
            foreach (var b in values.Where(b => !onlyActivity.Checked || HasActivity(b)).Reverse()) {
                int rowIndex = grid.Rows.Add(b.From.ToString(selectedGrouping == "Tháng" ? "MM/yyyy" : "dd/MM/yyyy"), TimeText(b.StudySeconds), TimeText(b.WorkSeconds), TimeText(b.FocusSeconds), b.Completed + " / " + b.Planned, b.EarnedMinutes.ToString("0.#"), b.RevokedMinutes.ToString("0.#"), b.EntertainmentMinutes.ToString("0.#"), b.GoalsDone + " / " + b.Goals);
                var row = grid.Rows[rowIndex]; row.Cells["period"].ToolTipText = b.From.ToString("dd/MM/yyyy") + " — " + b.To.AddDays(-1).ToString("dd/MM/yyyy");
                row.Cells["study"].ToolTipText = FocusPage.Duration(b.StudySeconds); row.Cells["work"].ToolTipText = FocusPage.Duration(b.WorkSeconds); row.Cells["total"].ToolTipText = FocusPage.Duration(b.FocusSeconds);
            }
            grid.ClearSelection(); empty.Visible = grid.Rows.Count == 0; grid.Visible = !empty.Visible;
            footer.Text = "Đang hiện " + grid.Rows.Count + "/" + values.Count + " kỳ · Thưởng: " + values.Sum(b => b.EarnedMinutes).ToString("0.#") + "p · Thu hồi: " + values.Sum(b => b.RevokedMinutes).ToString("0.#") + "p\nGiải trí cũ phân bổ gần đúng theo mốc lưu. Mục tiêu chỉ tính một lần mỗi tháng trong khoảng chọn.";
        }
        void SetDetailColumns() { foreach (var id in new[] { "earned", "revoked", "leisure", "goals" }) grid.Columns[id].Visible = moreDetails.Checked; Arrange(); }
        void Arrange() {
            if (layingOut || heading == null || apply == null) return; layingOut = true;
            try {
                int previousY = -scrollHost.AutoScrollPosition.Y;
                scrollHost.AutoScroll = false; content.Location = Point.Empty;
                int w = Math.Max(490, scrollHost.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 3); bool narrow = w < 760; content.Width = w;
                heading.SetBounds(0, 0, w, 48); filter.SetBounds(0, 56, w, 82);
                int presetX=10;int[] presetWidths={92,68,74,82,82};for (int i = 0; i < presets.Count; i++){presets[i].SetBounds(presetX,8,presetWidths[i],32);presetX+=presetWidths[i]+6;}
                more.SetBounds(presetX, 8, 32, 32); grouping.SetBounds(w - 208, narrow?48:9, 96, 30); apply.SetBounds(w - 102, narrow?48:8, 92, 32);
                range.SetBounds(12, 48, w - (narrow?230:24), 25); range.Visible = selectedPreset != "Tùy chọn";
                from.SetBounds(12, 48, 155, 28); to.SetBounds(180, 48, 155, 28);
                dateSeparator.SetBounds(166, 48, 16, 28);
                note.SetBounds(2, 142, w - 4, 26); int y = 174, columns = narrow ? 2 : 4, metricWidth = (w - (columns - 1) * 10) / columns;
                var metrics = new[] { focus, tasks, leisure, goals };
                for (int i = 0; i < metrics.Length; i++) metrics[i].SetBounds(i % columns * (metricWidth + 10), y + i / columns * 120, metricWidth, 110);
                y += narrow ? 240 : 120;
                int donutWidth = narrow ? w : 250;
                chartCard.SetBounds(0, y, narrow ? w : w - donutWidth - 12, 288); donutCard.SetBounds(narrow ? 0 : w - donutWidth, narrow ? y + 310 : y, donutWidth, narrow ? 286 : 288);
                y += narrow ? 608 : 300;
                detailHeading.SetBounds(2, y, w - 265, 35); onlyActivity.SetBounds(w - 251, y + 4, 120, 26); moreDetails.SetBounds(w - 125, y + 4, 125, 26); y += 40;
                tableCard.SetBounds(0, y, w, 150); y += 158; footer.SetBounds(2, y, w - 4, 45); content.Height = y + 48;
                scrollHost.AutoScrollMinSize = new Size(0, content.Height); scrollHost.AutoScroll = true; scrollHost.PerformLayout(); scrollHost.AutoScrollPosition = new Point(0, previousY);
            } finally { layingOut = false; }
        }
        public DailyStatisticsPage DailyForTest {get{return daily;}}
        public bool DailyVisibleForTest {get{return daily.Visible && !scrollHost.Visible;}}
        public int RowsForTest { get { return grid.Rows.Count; } }
        public int BucketsForTest { get { return values.Count; } }
        public double FocusSecondsForTest { get { return values.Sum(b => b.FocusSeconds); } }
        public int GoalsForTest { get { return values.Sum(b => b.Goals); } }
        public double ChartSecondsForTest { get { return bars.Buckets.Sum(b => b.FocusSeconds); } }
        public double DistributionSecondsForTest { get { return donut.Study + donut.Work; } }
        public int DetailColumnsForTest { get { return grid.Columns.Cast<DataGridViewColumn>().Count(c => c.Visible); } }
        public void ShowDetailsForTest(bool show) { moreDetails.Checked = show; }
        public bool FitsForTest { get { return content.Controls.Cast<Control>().All(c => c.Right <= content.Width) && !scrollHost.HorizontalScroll.Visible; } }
        public void PresetForTest(string value) { SetPreset(value); }
        public void ShowEmptyForTest(bool show) { onlyActivity.Checked = !show; }
        void SetGrouping(string value) { selectedGrouping = value; grouping.Text = "Theo " + value.ToLowerInvariant() + " ⌄"; RefreshData(); }
        public void GroupForTest(string value) { SetGrouping(value); }
        public void RangeForTest(DateTime begin, DateTime end) { SetPreset("Tùy chọn"); from.Value = begin; to.Value = end; RefreshData(); }
    }
}
