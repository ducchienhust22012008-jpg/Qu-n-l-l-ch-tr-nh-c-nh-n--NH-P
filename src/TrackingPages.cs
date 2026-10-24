using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Nhip {
    // A status ring, not a countdown or percentage of a target duration.
    sealed class FocusDial : Control {
        public string TimeText = "00:00:00", StatusText = "SẴN SÀNG";
        public bool Running, Paused;
        public FocusDial() { DoubleBuffered = true; BackColor = Color.White; AccessibleName = "Đồng hồ thời gian tập trung thực tế"; }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            int diameter = Math.Max(50, Math.Min(270, Math.Min(Width - 24, Height - 24)));
            var ring = new Rectangle((Width - diameter) / 2, (Height - diameter) / 2, diameter, diameter);
            Color accent = Running ? Theme.Purple : Paused ? Color.FromArgb(178, 121, 42) : Color.FromArgb(179, 158, 244);
            using (var brush = new SolidBrush(Color.FromArgb(251, 249, 255))) g.FillEllipse(brush, ring);
            using (var pen = new Pen(Color.FromArgb(237, 230, 253), 9)) g.DrawEllipse(pen, ring);
            using (var pen = new Pen(accent, 2)) g.DrawEllipse(pen, ring);
            using (var brush = new SolidBrush(Running ? Theme.Green : accent)) g.FillEllipse(brush, Width / 2 - 6, ring.Top - 6, 12, 12);
            using (var labelFont = Theme.Font(8, true)) using (var statusFont = Theme.Font(9, true)) {
                TextRenderer.DrawText(g, "THỜI GIAN THỰC", labelFont, new Rectangle(0, Height / 2 - 53, Width, 24), Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                float size = Math.Min(36, diameter / 7.1f);
                // Keep large accumulated hour counts inside the dial as well.
                size *= Math.Min(1, 8f / Math.Max(8, TimeText.Length));
                using (var digits = new Font("Consolas", size, FontStyle.Bold)) TextRenderer.DrawText(g, TimeText, digits, new Rectangle(8, Height / 2 - 25, Width - 16, 61), Theme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(g, StatusText, statusFont, new Rectangle(0, Height / 2 + 40, Width, 25), Running ? Theme.Green : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
    class FocusPage : UserControl {
        Store store; TextBox name = new TextBox(); string selectedCategory = "Học tập"; Button study, work; Label state, total, todayValue, weekValue, sessionsValue;
        FocusDial dial = new FocusDial(); Label historyEmpty;
        ComboBox taskChoice = new ComboBox(); Button taskCaption; bool bindingTasks;
        class FocusTaskChoice { public string Id, Caption; public override string ToString() { return Caption; } }
        double savedToday, savedWeek; DateTime summaryDay;
        TableLayoutPanel layout, heroLayout; Card face;
        Panel editor;
        Button start, pause, finish, discard; DataGridView history = Grids.Create();
        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        public Action Changed;
        public FocusPage(Store source) {
            store = source; Dock = DockStyle.Fill; BackColor = Theme.Bg;
            layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = new Padding(0), Padding = new Padding(0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 76, 78, 374, 47 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(layout);
            var heading = new Panel { Dock = DockStyle.Fill }; layout.Controls.Add(heading, 0, 0);
            var title = Theme.Label("Giữ nhịp tập trung", 25, Theme.Ink, true); title.Dock = DockStyle.Top; title.Height = 43; heading.Controls.Add(title);
            var hint = Theme.Label("Từng phút tập trung, từng bước tiến bộ.", 10, Theme.Muted); hint.Dock = DockStyle.Bottom; hint.Height = 28; heading.Controls.Add(hint);
            var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 12) };
            for (int i = 0; i < 3; i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            todayValue = SummaryCard(summary, 0, "TẬP TRUNG HÔM NAY", Theme.Purple);
            weekValue = SummaryCard(summary, 1, "7 NGÀY GẦN NHẤT", Theme.Ink);
            sessionsValue = SummaryCard(summary, 2, "PHIÊN ĐÃ LƯU", Theme.Green); layout.Controls.Add(summary, 0, 1);
            face = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4), Padding = new Padding(18) }; layout.Controls.Add(face, 0, 2);
            heroLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White, Margin = new Padding(0) };
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44)); heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); face.Controls.Add(heroLayout);
            dial.Dock = DockStyle.Fill; dial.Margin = new Padding(4, 4, 20, 4); heroLayout.Controls.Add(dial, 0, 0);
            editor = new Panel { Dock = DockStyle.Fill, Margin = new Padding(12, 0, 4, 0), BackColor = Color.White }; heroLayout.Controls.Add(editor, 1, 0);
            var sessionLabel = Theme.Label("CHỌN TASK CHƯA HOÀN THÀNH", 8, Theme.Purple, true); sessionLabel.SetBounds(0, 8, 300, 25); sessionLabel.AutoEllipsis = true; editor.Controls.Add(sessionLabel);
            name.Font = Theme.Font(10.5f); name.MaxLength = 160; name.BorderStyle = BorderStyle.FixedSingle; name.SetBounds(0, 98, 300, 28); editor.Controls.Add(name); name.AccessibleName = "Tên phiên tập trung";
            taskChoice.DropDownStyle = ComboBoxStyle.DropDownList;taskChoice.AccessibleName="Chọn task chưa hoàn thành";
            taskCaption=Theme.Button("Tự nhập nội dung / chọn task chưa hoàn thành");taskCaption.Font=Theme.Font(10);taskCaption.TextAlign=ContentAlignment.MiddleLeft;taskCaption.Padding=new Padding(4,0,0,0);taskCaption.AccessibleName="Chọn task chưa hoàn thành";taskCaption.SetBounds(0,42,300,34);((RoundedButton)taskCaption).DropDownIndicator=true;editor.Controls.Add(taskCaption);
            taskCaption.Click+=(s,e)=>{var menu=new ContextMenuStrip {Font=Theme.Font(10)};for(int i=0;i<taskChoice.Items.Count;i++){int n=i;var entry=menu.Items.Add(taskChoice.Items[i].ToString());entry.Tag=((FocusTaskChoice)taskChoice.Items[i]).Id;entry.Click+=(o,args)=>taskChoice.SelectedIndex=n;}taskCaption.ContextMenuStrip=menu;menu.Closed+=(o,args)=>{taskCaption.ContextMenuStrip=null;if(IsHandleCreated && !IsDisposed)BeginInvoke((Action)(()=>menu.Dispose()));else menu.Dispose();};menu.Show(taskCaption,new Point(0,taskCaption.Height));};
            taskChoice.SelectedIndexChanged += (s,e) => {
                taskCaption.Text=taskChoice.SelectedItem==null?"Tự nhập nội dung / chọn task chưa hoàn thành":taskChoice.SelectedItem.ToString();
                if (bindingTasks) return;
                var choice = taskChoice.SelectedItem as FocusTaskChoice;
                var task = choice == null ? null : store.AllTasks.FirstOrDefault(t => t.Id == choice.Id && !t.Done);
                if (task == null) return;
                bindingTasks = true; try { name.Text = task.Title; if (task.Category == "Học tập" || task.Category == "Công việc") selectedCategory = task.Category; } finally { bindingTasks = false; }
                RefreshClock();
            };
            name.TextChanged += (s,e) => { if (!bindingTasks && taskChoice.SelectedIndex > 0) { bindingTasks = true; taskChoice.SelectedIndex = 0; bindingTasks = false; } };
            var draftLabel=Theme.Label("Tên phiên (có thể sửa)",9,Theme.Muted);draftLabel.SetBounds(0,76,300,20);editor.Controls.Add(draftLabel);
            var categoryLabel = Theme.Label("Loại hoạt động", 9, Theme.Muted); categoryLabel.SetBounds(0, 134, 300, 23); editor.Controls.Add(categoryLabel);
            study = Theme.Button("Học tập"); work = Theme.Button("Công việc"); editor.Controls.Add(study); editor.Controls.Add(work);
            study.Click += (s, e) => { selectedCategory = "Học tập"; RefreshClock(); }; work.Click += (s, e) => { selectedCategory = "Công việc"; RefreshClock(); };
            state = Theme.Label("", 9, Theme.Muted); state.SetBounds(0, 203, 300, 38); state.AutoEllipsis = true; editor.Controls.Add(state);
            start = Theme.Button("Bắt đầu tập trung", true); pause = Theme.Button("Tạm dừng", true); finish = Theme.Button("Lưu phiên"); discard = Theme.Button("Bỏ phiên");
            foreach (var button in new[] { start, pause, finish, discard }) editor.Controls.Add(button);
            discard.FlatAppearance.BorderSize = 0; discard.Font = Theme.Font(9); discard.ForeColor = Color.FromArgb(153, 85, 98);
            Action arrangeEditor = () => {
                int w = Math.Max(20, editor.ClientSize.Width);
                sessionLabel.Width = taskChoice.Width = name.Width = draftLabel.Width = categoryLabel.Width = state.Width = w;
                taskCaption.Width=w;taskCaption.BringToFront();
                int split = Math.Max(10, (w - 10) / 2);
                study.SetBounds(0, 162, split, 34); work.SetBounds(split + 10, 162, split, 34);
                start.SetBounds(0, 247, split, 44); pause.SetBounds(0, 247, split, 44); finish.SetBounds(split + 10, 247, split, 44); discard.SetBounds(0, 300, w, 28);
            };
            editor.Resize += (s, e) => arrangeEditor(); arrangeEditor(); start.Font = pause.Font = finish.Font = Theme.Font(9, true);
            start.Click += (s, e) => Execute(() => { if (store.Data.ActiveFocus == null) store.StartFocus(name.Text, selectedCategory); else store.ResumeFocus(); });
            pause.Click += (s, e) => Execute(store.PauseFocus); finish.Click += (s, e) => Execute(store.FinishFocus);
            discard.Click += (s, e) => { if (MessageBox.Show(this, "Bỏ phiên hiện tại? Thời gian chưa lưu của phiên này sẽ không được đưa vào thống kê.", "Bỏ phiên bấm giờ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Execute(store.DiscardFocus); };
            total = Theme.Label("", 9, Theme.Muted, true); total.Dock = DockStyle.Fill; total.AutoEllipsis = true; layout.Controls.Add(total, 0, 3);
            history.Dock = DockStyle.Fill; history.ReadOnly = true; history.AllowUserToAddRows = false; history.AllowUserToDeleteRows = false;
            history.RowTemplate.Height = 48; history.ColumnHeadersHeight = 36; history.SelectionMode = DataGridViewSelectionMode.FullRowSelect; history.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 249, 255); history.DefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 234, 254); history.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            Grids.Column(history, "date", "Bắt đầu", 26, true); Grids.Column(history, "title", "Phiên đã lưu", 42, true); Grids.Column(history, "kind", "Loại", 18, true); Grids.Column(history, "duration", "Thời gian thực", 24, true);
            history.Columns["duration"].DefaultCellStyle.Font = new Font("Consolas", 10, FontStyle.Bold); history.Columns["duration"].DefaultCellStyle.ForeColor = Theme.Purple;
            history.Columns["duration"].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;history.Columns["duration"].HeaderCell.Style.Alignment=DataGridViewContentAlignment.MiddleRight;
            history.CellPainting+=(s,e)=>{if(e.RowIndex<0 || e.ColumnIndex!=history.Columns["kind"].Index)return;e.PaintBackground(e.ClipBounds,true);string text=Convert.ToString(e.FormattedValue);Color color=text=="Học tập"?Theme.Purple:Color.FromArgb(40,112,191);var r=new Rectangle(e.CellBounds.X+7,e.CellBounds.Y+9,Math.Max(16,Math.Min(e.CellBounds.Width-14,110)),Math.Max(18,e.CellBounds.Height-18));using(var path=Theme.Rounded(r,8))using(var brush=new SolidBrush(Theme.Tint(color,.09)))e.Graphics.FillPath(brush,path);TextRenderer.DrawText(e.Graphics,text,history.Font,r,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.Handled=true;};
            var historyCard = new Card { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(1) }; historyCard.Controls.Add(history); layout.Controls.Add(historyCard, 0, 4);
            historyEmpty = Theme.Label("Chưa có phiên đã lưu.\nBắt đầu phiên đầu tiên để ghi lại thời gian tập trung.", 10, Theme.Muted); historyEmpty.Dock = DockStyle.Fill; historyEmpty.TextAlign = ContentAlignment.MiddleCenter; historyCard.Controls.Add(historyEmpty);
            timer.Interval = 250; timer.Tick += (s, e) => RefreshClock(); timer.Start(); RefreshState();
        }
        void Execute(Action action) { try { action(); RefreshState(); if (Changed != null) Changed(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Bấm giờ", MessageBoxButtons.OK, MessageBoxIcon.Warning); RefreshState(); } }
        Label SummaryCard(TableLayoutPanel parent, int column, string caption, Color color) {
            var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(column == 0 ? 0 : 4, 0, column == 2 ? 0 : 4, 0) }; parent.Controls.Add(card, column, 0);
            var label = Theme.Label(caption, 7.5f, Theme.Muted, true); label.SetBounds(14, 5, 150, 23); label.AutoEllipsis = true; card.Controls.Add(label);
            var value = Theme.Label("", 16, color, true); value.SetBounds(14, 28, 150, 30); value.AutoEllipsis = true; card.Controls.Add(value);
            Action fit = () => { label.Width = value.Width = Math.Max(20, card.ClientSize.Width - 28); }; card.Resize += (s, e) => fit(); fit(); return value;
        }
        static double SecondsIn(IEnumerable<TimeSlice> slices, DateTime from, DateTime to) {
            long begin = from.ToUniversalTime().Ticks, end = to.ToUniversalTime().Ticks;
            return slices.Sum(s => Math.Max(0, Math.Min(end, s.End) - Math.Max(begin, s.Start)) / (double)TimeSpan.TicksPerSecond);
        }
        double ActiveSecondsIn(DateTime from, DateTime to) {
            var active = store.Data.ActiveFocus; if (active == null) return 0;
            double value = SecondsIn(active.Slices, from, to);
            if (active.Running) value += Math.Max(0, Math.Min(to.ToUniversalTime().Ticks, store.Clock().Ticks) - Math.Max(from.ToUniversalTime().Ticks, active.Anchor)) / (double)TimeSpan.TicksPerSecond;
            return value;
        }
        void RefreshSummary() {
            summaryDay = store.Clock().ToLocalTime().Date;
            savedToday = store.Data.FocusSessions.Sum(s => SecondsIn(s.Slices, summaryDay, summaryDay.AddDays(1)));
            savedWeek = store.Data.FocusSessions.Sum(s => SecondsIn(s.Slices, summaryDay.AddDays(-6), summaryDay.AddDays(1)));
            sessionsValue.Text = store.Data.FocusSessions.Count + " phiên";
        }
        public static string Duration(double seconds) { long value = (long)Math.Floor(Math.Max(0, seconds)); return (value / 3600).ToString("00") + ":" + (value / 60 % 60).ToString("00") + ":" + (value % 60).ToString("00"); }
        void RefreshClock() {
            var active = store.Data.ActiveFocus;
            dial.TimeText = Duration(store.FocusSeconds()); dial.Running = active != null && active.Running; dial.Paused = active != null && !active.Running;
            dial.StatusText = active == null ? "SẴN SÀNG BẮT ĐẦU" : active.Running ? "ĐANG TẬP TRUNG" : "ĐÃ TẠM DỪNG"; dial.AccessibleDescription = dial.TimeText + " · " + dial.StatusText; dial.Invalidate();
            if (summaryDay != store.Clock().ToLocalTime().Date) RefreshSummary();
            todayValue.Text = Duration(savedToday + ActiveSecondsIn(summaryDay, summaryDay.AddDays(1)));
            weekValue.Text = Duration(savedWeek + ActiveSecondsIn(summaryDay.AddDays(-6), summaryDay.AddDays(1)));
            state.Text = active == null ? "Đếm tăng thời gian thực.\nBắt đầu sẽ tạm dừng ví giải trí." : active.Running ? "Thời gian đang được ghi nhận.\nTạm dừng khi nghỉ, lưu phiên khi xong." : "Thời gian nghỉ không được tính.\nTiếp tục hoặc lưu phiên khi bạn sẵn sàng.";
            state.ForeColor = active != null && active.Running ? Theme.Green : Theme.Muted;
            start.Text = active == null ? "Bắt đầu tập trung" : "Tiếp tục"; start.Enabled = active == null || !active.Running;
            start.Visible = active == null || !active.Running; pause.Visible = active != null && active.Running;
            pause.Enabled = active != null && active.Running; finish.Enabled = active != null && store.FocusSeconds() >= 1; discard.Enabled = active != null; discard.Visible = active != null;
            taskCaption.Enabled = taskChoice.Enabled = name.Enabled = study.Enabled = work.Enabled = active == null;
            study.BackColor = selectedCategory == "Học tập" ? Theme.Sidebar : Color.White; study.ForeColor = selectedCategory == "Học tập" ? Theme.Purple : Theme.Muted;
            work.BackColor = selectedCategory == "Công việc" ? Theme.Sidebar : Color.White; work.ForeColor = selectedCategory == "Công việc" ? Theme.Purple : Theme.Muted;
            study.FlatAppearance.BorderColor = selectedCategory == "Học tập" ? Theme.Purple : Theme.Line; work.FlatAppearance.BorderColor = selectedCategory == "Công việc" ? Theme.Purple : Theme.Line;
        }
        void RefreshTaskChoices() {
            var previous = taskChoice.SelectedItem as FocusTaskChoice; string selected = previous == null ? null : previous.Id;
            bindingTasks = true; taskChoice.BeginUpdate();
            try {
                taskChoice.Items.Clear(); taskChoice.Items.Add(new FocusTaskChoice { Caption = "Tự nhập nội dung / chọn task chưa hoàn thành", Id = null });
                foreach (var t in store.AllTasks.Where(t => !t.Done).OrderBy(t => store.IsScheduled(t.Id) ? 0 : 1).ThenBy(t => t.Day).ThenBy(t => t.Time).ThenBy(t => t.Title)) {
                    taskChoice.Items.Add(new FocusTaskChoice { Id = t.Id, Caption = t.Title + "  ·  " + (store.IsScheduled(t.Id) ? DateTime.ParseExact(t.Day,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("dd/MM/yyyy") + " " + t.Time : "Chưa xếp lịch") });
                }
                taskChoice.SelectedIndex = 0;
                for (int i=1;i<taskChoice.Items.Count;i++) if (((FocusTaskChoice)taskChoice.Items[i]).Id == selected) taskChoice.SelectedIndex=i;
            } finally { taskChoice.EndUpdate(); bindingTasks = false; }
        }
        public void RefreshState() {
            RefreshTaskChoices();
            if (store.Data.ActiveFocus != null) { bindingTasks=true; try { name.Text = store.Data.ActiveFocus.Title; selectedCategory = store.Data.ActiveFocus.Category; } finally { bindingTasks=false; } }
            history.Rows.Clear();
            foreach (var session in store.Data.FocusSessions.OrderByDescending(s => s.Created).Take(200)) {
                int row = history.Rows.Add(new DateTime(session.Created, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm"), session.Title, session.Category, Duration(Store.SliceSeconds(session.Slices)));
                history.Rows[row].Cells[1].ToolTipText = session.Title;
                history.Rows[row].Cells[2].Style.ForeColor = session.Category == "Học tập" ? Theme.Purple : Theme.Green;
            }
            history.ClearSelection(); historyEmpty.Visible = history.Rows.Count == 0; history.Visible = !historyEmpty.Visible;
            total.Text = "PHIÊN GẦN ĐÂY  ·  " + store.Data.FocusSessions.Count + " phiên đã lưu  ·  " + (store.Data.FocusSessions.Sum(s => Store.SliceSeconds(s.Slices)) / 3600).ToString("0.##") + " giờ  ·  Hiển thị tối đa 200 phiên";
            RefreshSummary();
            RefreshClock();
        }
        protected override void Dispose(bool disposing) { if (disposing) {timer.Dispose();taskChoice.Dispose();} base.Dispose(disposing); }
        public void ChooseTaskThroughMenuForTest(string id) { taskCaption.PerformClick();var menu=taskCaption.ContextMenuStrip;((ToolStripMenuItem)menu.Items.Cast<ToolStripItem>().First(i=>Convert.ToString(i.Tag)==id)).PerformClick();if(!menu.IsDisposed)menu.Close(); }
        public void ChooseTaskForTest(string id) { for (int i=0;i<taskChoice.Items.Count;i++) if (((FocusTaskChoice)taskChoice.Items[i]).Id == id) { taskChoice.SelectedIndex=i;return; } throw new InvalidOperationException("Task not offered"); }
        public bool OffersTaskForTest(string id) { return taskChoice.Items.Cast<FocusTaskChoice>().Any(c => c.Id == id); }
        public string DraftTitleForTest { get { return name.Text; } }
        public string DraftCategoryForTest { get { return selectedCategory; } }
        public string SelectedTaskForTest { get { return ((FocusTaskChoice)taskChoice.SelectedItem).Id; } }
        public bool TaskPickerEnabledForTest { get { return taskChoice.Enabled; } }
        public void SetDraftForTest(string text) { name.Text=text; }
        public void StartSelectedForTest() { start.PerformClick(); }
        public void StartForTest() { name.Text = "Phiên qua giao diện"; start.PerformClick(); }
        public void PauseForTest() { pause.PerformClick(); }
        public void ResumeForTest() { start.PerformClick(); }
        public void FinishForTest() { RefreshClock(); finish.PerformClick(); }
        public bool IdleUiForTest { get { return start.Visible && start.Enabled && !pause.Visible && !finish.Enabled && !discard.Visible && name.Enabled && study.Enabled && work.Enabled && dial.TimeText == "00:00:00"; } }
        public bool RunningUiForTest { get { return !start.Visible && pause.Visible && pause.Enabled && !name.Enabled && !study.Enabled && !work.Enabled && dial.Running; } }
        public bool PausedUiForTest { get { return start.Visible && start.Text == "Tiếp tục" && !pause.Visible && finish.Enabled && discard.Visible && dial.Paused; } }
        public bool FitsForTest { get { return editor.Controls.Cast<Control>().All(c => c.Right <= editor.ClientSize.Width && c.Bottom <= editor.ClientSize.Height) && dial.Width >= 190 && history.Parent.ClientSize.Height >= 70; } }
        public string LayoutForTest { get { return "editor=" + editor.ClientSize + " dial=" + dial.Size + " history=" + history.Size + " controls=" + String.Join(";", editor.Controls.Cast<Control>().Where(c => c.Right > editor.ClientSize.Width || c.Bottom > editor.ClientSize.Height).Select(c => c.Text + " " + c.Bounds)); } }
        public string TodayTimeForTest { get { return todayValue.Text; } }
        public void ChooseWorkForTest() { work.PerformClick(); }
    }
    class WeekLookupDialog : BrandedForm {
        DateTimePicker date = new DateTimePicker(); DataGridView weeks = Grids.Create(); public DateTime SelectedWeek;
        public WeekLookupDialog(AppData data, DateTime current) {
            Text = "Tra cứu tuần"; ClientSize = new Size(620, 560); StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; Font = Theme.Font(10); AutoScaleMode = AutoScaleMode.Dpi;
            var title = Theme.Label("Tra cứu tuần trước", 23, Theme.Ink, true); title.SetBounds(24, 16, 570, 48); Controls.Add(title);
            var hint = Theme.Label("Chọn một ngày để mở tuần tương ứng hoặc nhấp đúp tuần trong danh sách.", 10, Theme.Muted); hint.SetBounds(26, 70, 568, 48); Controls.Add(hint);
            date.Format = DateTimePickerFormat.Custom; date.CustomFormat = "dd/MM/yyyy"; date.Value = current; date.SetBounds(26, 127, 200, 32); Controls.Add(date);
            var open = Theme.Button("Xem tuần này", true); open.SetBounds(242, 123, 170, 38); open.Click += (s, e) => Open(date.Value); Controls.Add(open);
            weeks.SetBounds(26, 182, 568, 298); weeks.ReadOnly = true; weeks.AllowUserToAddRows = false; weeks.AllowUserToDeleteRows = false; weeks.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Grids.Column(weeks, "week", "Tuần đã lưu", 55, true); Grids.Column(weeks, "tasks", "Công việc", 22, true); Grids.Column(weeks, "done", "Đã xong", 22, true);
            var starts = new HashSet<DateTime>(data.Tasks.Select(t => Store.WeekOf(DateTime.ParseExact(t.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture))));
            foreach (var s in data.FocusSessions) starts.Add(Store.WeekOf(new DateTime(s.Created, DateTimeKind.Utc).ToLocalTime()));
            for (int i = 0; i < 52; i++) starts.Add(Store.WeekOf(DateTime.Today).AddDays(-7 * i));
            foreach (var first in starts.OrderByDescending(d => d)) {
                string begin = first.ToString("yyyy-MM-dd"), end = first.AddDays(7).ToString("yyyy-MM-dd"); var tasks = data.Tasks.Where(t => String.CompareOrdinal(t.Day, begin) >= 0 && String.CompareOrdinal(t.Day, end) < 0).ToList();
                int index = weeks.Rows.Add(first.ToString("dd/MM/yyyy") + " – " + first.AddDays(6).ToString("dd/MM/yyyy"), tasks.Count, tasks.Count(t => t.Done)); weeks.Rows[index].Tag = first;
            }
            weeks.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Open((DateTime)weeks.Rows[e.RowIndex].Tag); }; Controls.Add(weeks);
            var close = Theme.Button("Đóng"); close.SetBounds(474, 503, 120, 38); close.DialogResult = DialogResult.Cancel; Controls.Add(close); CancelButton = close;
        }
        void Open(DateTime day) { SelectedWeek = Store.WeekOf(day); DialogResult = DialogResult.OK; Close(); }
        public void ChooseForTest(DateTime day) { date.Value = day; Open(date.Value); }
    }
}
