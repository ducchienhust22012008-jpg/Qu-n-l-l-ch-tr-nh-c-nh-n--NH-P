using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Nhip {
    static class Theme {
        public static Color Bg = Color.FromArgb(248, 249, 253), Ink = Color.FromArgb(30, 38, 60), Muted = Color.FromArgb(107, 119, 146), Purple = Color.FromArgb(112, 77, 232), Line = Color.FromArgb(230, 233, 244), Green = Color.FromArgb(27, 131, 108), Sidebar = Color.FromArgb(243, 240, 255);
        public static Font Font(float size, bool bold = false) { return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular); }
        public static Label Label(string text, float size, Color color, bool bold = false) { return new Label { Text = text, Font = Font(size, bold), ForeColor = color, AutoSize = false, UseMnemonic=false, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft }; }
        public static Button Button(string text, bool primary = false) {
            var b = new RoundedButton { Text = text, Font = Font(10, true), BackColor = primary ? Purple : Color.White, ForeColor = primary ? Color.White : Ink, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Height = 42, Margin = new Padding(0), UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderSize = primary ? 0 : 1; b.FlatAppearance.BorderColor = Line;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(91, 64, 199) : Color.FromArgb(240, 238, 252);
            return b;
        }
        public static Color CategoryColor(string category) {
            if (category == "Học tập") return Purple;
            if (category == "Vận động") return Green;
            if (category == "Công việc") return Color.FromArgb(40, 112, 191);
            return Color.FromArgb(184, 113, 34);
        }
        public static Color TaskColor(TaskItem task) { return String.IsNullOrEmpty(task.ColorHex) ? CategoryColor(task.Category) : ColorTranslator.FromHtml(task.ColorHex); }
        public static Color Tint(Color color, double amount) { return Color.FromArgb((int)(255 * (1 - amount) + color.R * amount), (int)(255 * (1 - amount) + color.G * amount), (int)(255 * (1 - amount) + color.B * amount)); }
        public static GraphicsPath Rounded(Rectangle r, int radius) {
            var path = new GraphicsPath(); int d = radius * 2;
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
    }
    sealed class BrandLogo : Control {
        readonly Image artwork;
        public BrandLogo() {
            using (var stream = typeof(BrandLogo).Assembly.GetManifestResourceStream("Nhip.Brand.png"))
            using (var image = Image.FromStream(stream)) artwork = new Bitmap(image);
            DoubleBuffered = true;
            AccessibleName = "Nhịp — Thời gian của bạn";
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            // Display the light-background lockup from the supplied brand sheet.
            // Keep the original image embedded without changing its artwork.
            var source = new Rectangle(200, 720, 545, 225);
            float scale = Math.Min((float)Width / source.Width, (float)Height / source.Height);
            var target = new RectangleF((Width - source.Width * scale) / 2, (Height - source.Height * scale) / 2, source.Width * scale, source.Height * scale);
            e.Graphics.DrawImage(artwork, target, source, GraphicsUnit.Pixel);
        }
        protected override void Dispose(bool disposing) {
            if (disposing) artwork.Dispose();
            base.Dispose(disposing);
        }
    }
    class Card : Panel {
        public Color Accent = Color.Empty;
        public Card() { BackColor = Color.White; DoubleBuffered = true; Padding = new Padding(16); }
        protected override void OnPaintBackground(PaintEventArgs e) {
            e.Graphics.Clear(Parent == null ? Theme.Bg : Parent.BackColor);
            if (Width < 18 || Height < 18) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (var brush = new SolidBrush(BackColor)) e.Graphics.FillPath(brush, path);
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            if (Width < 18 || Height < 18) return;
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (var pen = new Pen(Theme.Line)) e.Graphics.DrawPath(pen, path);
            if (!Accent.IsEmpty) using (var brush = new SolidBrush(Accent)) e.Graphics.FillRectangle(brush, 0, 0, 3, Height);
        }
    }
    class TaskDialog : BrandedForm {
        TextBox title = new TextBox(); DateTimePicker day = new DateTimePicker(), time = new DateTimePicker();
        NumericUpDown duration = new NumericUpDown(), reward = new NumericUpDown(); ComboBox category = new ComboBox();
        TaskItem original; string colorHex; Button colorButton;
        DeadlineSchedulePanel deadlineSource; bool usingDeadlineSource;
        public bool UsesDeadlineSource {get{return usingDeadlineSource;}}
        public bool RescheduleConfirmed {get{return deadlineSource!=null && deadlineSource.RescheduleConfirmed;}}
        public DeadlineSchedulePanel DeadlineSourceForTest {get{return deadlineSource;}}
        Action<bool> sourceSelector;
        public void SelectSourceForTest(bool fromDeadline){if(sourceSelector==null)throw new InvalidOperationException("Source selector unavailable");sourceSelector(fromDeadline);}
        readonly Store taskStore; ComboBox deadlineChoice = new ComboBox(); Label deadlineHint;
        class DeadlineChoice { public string Id, Caption; public override string ToString() { return Caption; } }
        public string SelectedDeadlineId { get { if(usingDeadlineSource)return deadlineSource.SelectedDeadlineId; var choice = deadlineChoice.SelectedItem as DeadlineChoice; return choice == null ? null : choice.Id; } }
        public TaskItem Result { get; private set; }
        public TaskDialog(TaskItem task, DateTime selected, int defaultReward = 15, bool preciseTime = false, bool copying = false, Store deadlines = null, bool allowDeadlineSource = false) {
            taskStore = deadlines; original = task; Text = copying ? "Sao chép công việc" : task == null ? "Thêm công việc" : "Chỉnh sửa công việc";
            ClientSize = new Size(620, 660); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; Font = Theme.Font(10); AutoScaleMode = AutoScaleMode.Dpi;
            var heading = Theme.Label(Text, 21, Theme.Ink, true); heading.SetBounds(26, 16, 440, 48); Controls.Add(heading);
            var sub = Theme.Label("Lên lịch rõ ràng. Nhận thưởng khi hoàn thành.", 10, Theme.Muted); sub.SetBounds(28, 65, 440, 25); Controls.Add(sub);
            AddField("Tên công việc", title, 106); title.MaxLength = 160;
            AddField("Ngày thực hiện", day, 178); day.Format = DateTimePickerFormat.Custom; day.CustomFormat = "dd/MM/yyyy";
            AddField("Giờ bắt đầu", time, 250); time.Format = DateTimePickerFormat.Custom; time.CustomFormat = "HH:mm"; time.ShowUpDown = true;
            time.Width = 200;
            var dl = Theme.Label("Thời lượng (phút)", 10, Theme.Ink); dl.SetBounds(264, 250, 198, 24); Controls.Add(dl);
            duration.SetBounds(264, 278, 198, 30); duration.Minimum = 1; duration.Maximum = 1440; duration.Value = 60; Controls.Add(duration);
            AddField("Nhóm công việc", category, 322); category.DropDownStyle = ComboBoxStyle.DropDownList;
            category.Items.AddRange(new object[] { "Học tập", "Công việc", "Vận động", "Cá nhân" }); category.SelectedIndex = 0; category.Width = 200;
            var rl = Theme.Label("Phút giải trí được thưởng", 10, Theme.Ink); rl.SetBounds(264, 322, 210, 24); Controls.Add(rl);
            reward.SetBounds(264, 350, 198, 30); reward.Minimum = 0; reward.Maximum = 600; reward.Value = defaultReward; Controls.Add(reward);
            var hint = Theme.Label(task != null && task.Done ? "Việc đã hoàn thành giữ nguyên phần thưởng đã nhận.\nMức thưởng mới áp dụng khi bỏ tick rồi hoàn thành lại." : "Tick hoàn thành sẽ cộng phút vào ví thời gian.\nBạn có thể đặt 0 phút cho việc không có thưởng.", 9, Theme.Muted);
            var colorLabel = Theme.Label("Màu khối công việc", 10, Theme.Ink); colorLabel.SetBounds(28, 397, 225, 36); Controls.Add(colorLabel);
            colorHex = task == null ? null : task.ColorHex;
            colorButton = Theme.Button("Chọn màu…"); colorButton.SetBounds(264, 397, 198, 38); colorButton.Click += (s, e) => { using (var dialog = new TaskColorDialog(colorHex)) if (dialog.ShowDialog(this) == DialogResult.OK) { colorHex = dialog.ColorHex; RefreshColor(); } }; Controls.Add(colorButton); RefreshColor();
            Controls.OfType<Label>().First(l => l.Text == "Giờ bắt đầu").Width = 200;
            Controls.OfType<Label>().First(l => l.Text == "Nhóm công việc").Width = 200;
            AddField("Deadline", deadlineChoice, 449); deadlineChoice.DropDownStyle = ComboBoxStyle.DropDownList; deadlineChoice.DropDownWidth = 600; deadlineChoice.IntegralHeight = false; deadlineChoice.MaxDropDownItems = 8;
            deadlineChoice.Items.Add(new DeadlineChoice { Caption = "Không gắn deadline", Id = null });
            string currentParent = taskStore == null || task == null || copying ? null : (taskStore.ParentDeadline(task.Id) == null ? null : taskStore.ParentDeadline(task.Id).Id);
            if (taskStore != null) foreach (var d in taskStore.Data.Deadlines.Where(d => !d.Completed || d.Id == currentParent).OrderBy(d => d.Due)) deadlineChoice.Items.Add(new DeadlineChoice { Id = d.Id, Caption = d.Title + (d.Completed ? " · đã hoàn tất" : "") });
            deadlineHint = Theme.Label("", 8.5f, Theme.Muted); deadlineHint.SetBounds(28, 511, 434, 22); deadlineHint.AutoEllipsis = true; Controls.Add(deadlineHint);
            deadlineChoice.SelectedIndexChanged += (s,e) => { var d = taskStore == null ? null : taskStore.Data.Deadlines.FirstOrDefault(x => x.Id == SelectedDeadlineId); deadlineHint.Text = d == null ? "Chọn deadline để gắn trực tiếp block này khi lưu." : "Hạn: " + d.Due.ToString("dd/MM/yyyy") + (String.IsNullOrEmpty(d.DueTime) ? " · cuối ngày" : " · " + d.DueTime); };
            deadlineChoice.SelectedIndex = 0;
            for (int i = 1; i < deadlineChoice.Items.Count; i++) if (((DeadlineChoice)deadlineChoice.Items[i]).Id == currentParent) deadlineChoice.SelectedIndex = i;
            hint.SetBounds(28, 539, 434, 43); Controls.Add(hint);
            var cancel = Theme.Button("Hủy"); cancel.SetBounds(244, 594, 92, 43); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel); CancelButton = cancel;
            var save = Theme.Button("Lưu công việc", true); save.SetBounds(346, 594, 116, 43); save.Click += SaveClicked; Controls.Add(save); AcceptButton = save;
            day.Value = selected.Date; time.Value = DateTime.Today.Add(!preciseTime && selected.TimeOfDay == TimeSpan.Zero ? TimeSpan.FromHours(9) : selected.TimeOfDay);
            if (task != null) {
                title.Text = task.Title; day.Value = DateTime.ParseExact(task.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                time.Value = DateTime.Today.Add(TimeSpan.Parse(task.Time)); duration.Value = task.Duration; reward.Value = task.Reward;
                if (!category.Items.Contains(task.Category)) category.Items.Add(task.Category); category.SelectedItem = task.Category;
            }
            ArrangeModernForm(heading, sub, hint);
            if(task==null && !copying && taskStore!=null && allowDeadlineSource)InstallDeadlineSource(selected,preciseTime,sub,save,cancel);
            Shown += (s, e) => title.Focus();
        }
        void InstallDeadlineSource(DateTime selected,bool precise,Label subtitle,Button save,Button cancel){
            var fields=Controls.Cast<Control>().Where(c=>c.Top>=98).ToList();foreach(var control in fields)control.Top+=46;
            ClientSize=new Size(620,706);subtitle.Text="Chọn nguồn công việc rồi đặt lịch thực hiện.";
            var create=Theme.Button("Tạo việc mới",true);create.SetBounds(28,94,245,36);Controls.Add(create);
            var choose=Theme.Button("Chọn việc từ Deadline");choose.SetBounds(285,94,307,36);Controls.Add(choose);
            deadlineSource=new DeadlineSchedulePanel(taskStore,selected,precise);deadlineSource.Location=new Point(0,140);deadlineSource.Visible=false;Controls.Add(deadlineSource);
            deadlineSource.Prepared+=()=>{Result=deadlineSource.Result;DialogResult=DialogResult.OK;Close();};
            sourceSelector=fromDeadline=>{usingDeadlineSource=fromDeadline;foreach(var control in fields)control.Visible=!fromDeadline;deadlineSource.Visible=fromDeadline;if(fromDeadline)deadlineSource.BringToFront();create.BackColor=fromDeadline?Color.White:Theme.Purple;create.ForeColor=fromDeadline?Theme.Ink:Color.White;choose.BackColor=fromDeadline?Theme.Purple:Color.White;choose.ForeColor=fromDeadline?Color.White:Theme.Ink;AcceptButton=fromDeadline?deadlineSource.SaveButton:save;CancelButton=fromDeadline?deadlineSource.CancelAction:cancel;};
            create.Click+=(s,e)=>sourceSelector(false);choose.Click+=(s,e)=>sourceSelector(true);
        }
        void ArrangeModernForm(Label heading,Label sub,Label hint) {
            foreach(var l in Controls.OfType<Label>().ToArray()) if(l!=heading && l!=sub && l!=hint && l!=deadlineHint)l.Dispose();
            heading.SetBounds(28,14,564,44);sub.SetBounds(30,62,564,25);
            AddSection("1", "Thông tin",98);AddSection("2", "Lịch thực hiện",209);AddSection("3", "Deadline & phần thưởng",322);
            ModernField("Tên công việc",title,28,133,350);ModernField("Nhóm công việc",category,400,133,192);
            ModernField("Ngày",day,28,245,238);ModernField("Giờ bắt đầu",time,282,245,142);ModernField("Thời lượng (phút)",duration,440,245,152);
            ModernField("Deadline liên quan",deadlineChoice,28,357,350);ModernField("Phút thưởng",reward,400,357,192);
            deadlineHint.SetBounds(28,419,564,25);
            var colorLabel=Theme.Label("Màu công việc",10,Theme.Ink);colorLabel.SetBounds(28,463,200,30);Controls.Add(colorLabel);colorButton.SetBounds(210,459,185,36);
            string[] colors={"#704DE8","#16877D","#B87122","#C84143"};
            for(int i=0;i<colors.Length;i++){string hex=colors[i];var b=Theme.Button("");b.SetBounds(412+i*46,461,34,32);b.BackColor=ColorTranslator.FromHtml(hex);b.AccessibleName="Màu "+hex;b.Click+=(o,e)=>{colorHex=hex;RefreshColor();};Controls.Add(b);}
            hint.SetBounds(28,518,564,48);hint.BackColor=Theme.Sidebar;hint.Padding=new Padding(10,0,8,0);hint.Font=Theme.Font(9);
            Controls.OfType<Button>().First(b=>b.Text=="Hủy").SetBounds(328,594,110,42);Controls.OfType<Button>().First(b=>b.Text=="Lưu công việc").SetBounds(450,594,142,42);
        }
        void AddSection(string number,string text,int y){
            var badge=new PillLabel { Text=number,BackColor=Theme.Sidebar,ForeColor=Theme.Purple,Font=Theme.Font(11,true),TextAlign=ContentAlignment.MiddleCenter };badge.SetBounds(28,y,30,30);Controls.Add(badge);
            var l=Theme.Label(text,12,Theme.Ink,true);l.SetBounds(70,y,490,30);Controls.Add(l);
        }
        void ModernField(string caption,Control control,int x,int y,int width){var l=Theme.Label(caption,9.5f,Theme.Muted);l.SetBounds(x,y,width,24);Controls.Add(l);control.SetBounds(x,y+28,width,30);control.Font=Theme.Font(10.5f);}
        void AddField(string text, Control c, int y) {
            var label = Theme.Label(text, 10, Theme.Ink); label.SetBounds(28, y, 434, 24); Controls.Add(label);
            c.SetBounds(28, y + 28, 434, 30); c.Font = Theme.Font(11); Controls.Add(c);
        }
        void SaveClicked(object sender, EventArgs args) {
            if (String.IsNullOrWhiteSpace(title.Text)) { MessageBox.Show(this, "Hãy nhập tên công việc.", "Thiếu tên công việc", MessageBoxButtons.OK, MessageBoxIcon.Information); title.Focus(); return; }
            Result = new TaskItem { Id = original == null ? Guid.NewGuid().ToString("N") : original.Id, Title = title.Text.Trim(), Day = day.Value.ToString("yyyy-MM-dd"), Time = time.Value.ToString("HH:mm"), Duration = (int)duration.Value, Reward = (int)reward.Value, Category = category.Text, ColorHex = colorHex };
            var parent = taskStore == null ? null : taskStore.Data.Deadlines.FirstOrDefault(d => d.Id == SelectedDeadlineId);
            if (parent != null && DateTime.ParseExact(Result.Day + " " + Result.Time, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).AddMinutes(Result.Duration) > parent.Due && MessageBox.Show(this, "Block công việc kết thúc sau hạn deadline “" + parent.Title + "”. Vẫn lưu? Hạn deadline sẽ giữ nguyên.", "Lịch sau hạn", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            DialogResult = DialogResult.OK; Close();
        }
        public void SelectDeadlineForTest(string id) { for (int i=0;i<deadlineChoice.Items.Count;i++) if (((DeadlineChoice)deadlineChoice.Items[i]).Id == id) { deadlineChoice.SelectedIndex=i;return; } throw new InvalidOperationException("Missing deadline choice"); }
        public int DeadlineChoicesForTest { get { return deadlineChoice.Items.Count; } }
        public void FillForTest(string text, int minutes) { title.Text = text; reward.Value = minutes; Controls.OfType<Button>().First(b => b.Text == "Lưu công việc").PerformClick(); }
        public void SaveForTest() { Controls.OfType<Button>().First(b => b.Text == "Lưu công việc").PerformClick(); }
        public int RewardForTest { get { return (int)reward.Value; } }
        void RefreshColor() { colorButton.BackColor = String.IsNullOrEmpty(colorHex) ? Color.White : Theme.Tint(ColorTranslator.FromHtml(colorHex), .2); colorButton.Text = String.IsNullOrEmpty(colorHex) ? "Màu theo nhóm" : colorHex + " · Đổi màu"; }
    }
    class MainForm : BrandedForm {
        Store store; bool weekView = true; DateTime weekStart; FlowLayoutPanel body; Panel center;
        DateTime selectedDay = DateTime.Today;
        MonthlyGoalsCard monthlyGoals;
        Button goalsToggle;
        bool statsGoalsOpen;
        bool overdueExpanded;
        bool resizingBody;
        PlanningProgress todayProgress, monthProgress;
        Panel planningToolbar;
        Label goalSummary, completeCaption, earnedCaption, motivation;
        Label title, subtitle, balance, timerState, walletNote, earned, complete;
        Button todayButton, weekButton, play;
        Button focusButton, statisticsButton, deadlineButton; int activePage;
        TableLayoutPanel root; Panel rightRail; DeadlinePage deadlinePage;
        TableLayoutPanel planningLayout; Panel extraPageHost; FocusPage focusPage; StatisticsPage statisticsPage;
        WeekCalendar calendar; int calendarViewMinute = 420;
        DateTime goalsMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        Button goalsEdit, settingsButton;
        System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer();
        ToolTip tips = new ToolTip { AutoPopDelay = 15000, InitialDelay = 350 };
        string lastDay; bool expiryShown;
        public MainForm(Store data) {
            store = data; weekStart = Monday(DateTime.Today); lastDay = Key(DateTime.Today);
            Text = "Nhịp — Lịch tuần & ví thời gian"; Font = Theme.Font(10); BackColor = Theme.Bg; AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(1160, 740); Size = new Size(1450, 920); StartPosition = FormStartPosition.CenterScreen;
            var area = Screen.PrimaryScreen.WorkingArea; if (Height > area.Height) Height = area.Height; if (Width > area.Width) Width = area.Width;
            root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Bg };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 312));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(root);
            var side = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Margin = new Padding(0) }; root.Controls.Add(side, 0, 0);
            var brand = new BrandLogo(); brand.SetBounds(12, 25, 156, 80); side.Controls.Add(brand);
            todayButton = Nav("Hôm nay", 150, side); todayButton.Click += (s, e) => { activePage = 0; weekView = false; Render(); };
            weekButton = Nav("Lịch tuần", 204, side); weekButton.Click += (s, e) => { activePage = 0; weekView = true; Render(); };
            deadlineButton = Nav("Deadline", 258, side); deadlineButton.Click += (s,e) => { activePage = 3; deadlinePage.ShowOverview(0); Render(); };
            var lookup = Nav("Tra cứu tuần", 312, side); lookup.Click += (s, e) => OpenWeekLookup();
            focusButton = Nav("Bấm giờ", 366, side); focusButton.Click += (s, e) => { activePage = 1; Render(); };
            statisticsButton = Nav("Thống kê", 420, side); statisticsButton.Click += (s, e) => { activePage = 2; Render(); };
            settingsButton = Nav("Cài đặt", 474, side); settingsButton.Click += (s, e) => OpenSettings();
            var local = Theme.Label("●  Lưu trên máy\n\nNhịp 2.9\nLàm việc có nhịp.\nNghỉ ngơi có chủ đích.", 9, Theme.Muted); local.SetBounds(22, side.Height - 150, 142, 130); local.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; side.Controls.Add(local);
            center = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 20, 16, 18), Margin = new Padding(0) }; root.Controls.Add(center, 1, 0);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0) };
            planningLayout = layout;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 106)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); center.Controls.Add(layout);
            var head = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }; layout.Controls.Add(head, 0, 0);
            title = Theme.Label("", 26, Theme.Ink, true); title.SetBounds(0, 0, 370, 49); head.Controls.Add(title);
            title.AutoEllipsis = true; head.Resize += (s, e) => title.Width = Math.Max(180, head.ClientSize.Width - 152);
            subtitle = Theme.Label("", 10, Theme.Muted); subtitle.SetBounds(1, 47, 550, 26); subtitle.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right; head.Controls.Add(subtitle);
            motivation = Theme.Label("", 9, Theme.Muted); motivation.SetBounds(1, 74, 550, 23); motivation.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right; motivation.AutoEllipsis = true; head.Controls.Add(motivation);
            var add = Theme.Button("+ Thêm việc", true); add.Size = new Size(132, 42); add.Location = new Point(head.Width - 132, 6); add.Anchor = AnchorStyles.Top | AnchorStyles.Right; add.Click += (s, e) => EditTask(null, DateTime.Today); head.Controls.Add(add);
            var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 8) };
            for (int i = 0; i < 3; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3)); layout.Controls.Add(stats, 0, 1);
            complete = Stat(stats, 0, "CÔNG VIỆC TUẦN NÀY", Theme.Ink); completeCaption = complete.Parent.Controls.OfType<Label>().First(l => l != complete);
            earned = Stat(stats, 1, "PHẦN THƯỞNG TUẦN NÀY", Theme.Green); earnedCaption = earned.Parent.Controls.OfType<Label>().First(l => l != earned);
            goalSummary = Stat(stats, 2, "MỤC TIÊU THÁNG NÀY", Theme.Purple);
            todayProgress = AddProgress(complete.Parent); monthProgress = AddProgress(goalSummary.Parent);
            var toolbar = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }; layout.Controls.Add(toolbar, 0, 2);
            planningToolbar = toolbar;
            var section = Theme.Label("LỊCH & CÔNG VIỆC", 9, Theme.Muted, true); section.SetBounds(0, 5, 180, 32); toolbar.Controls.Add(section);
            var prev = Theme.Button("‹"); var next = Theme.Button("›"); var current = Theme.Button("Tuần này");
            prev.SetBounds(toolbar.Width - 186, 5, 36, 34); current.SetBounds(toolbar.Width - 144, 5, 100, 34); next.SetBounds(toolbar.Width - 38, 5, 36, 34);
            foreach (Button b in new[] { prev, current, next }) { b.Anchor = AnchorStyles.Right | AnchorStyles.Top; toolbar.Controls.Add(b); }
            prev.Click += (s, e) => { activePage = 0; weekView = true; weekStart = weekStart.AddDays(-7); Render(); };
            next.Click += (s, e) => { activePage = 0; weekView = true; weekStart = weekStart.AddDays(7); Render(); };
            current.Click += (s, e) => { activePage = 0; weekView = true; weekStart = Monday(DateTime.Today); Render(); };
            var chooseWeek = Theme.Button("Chọn tuần…"); chooseWeek.SetBounds(toolbar.Width - 315, 5, 120, 34); chooseWeek.Anchor = AnchorStyles.Top | AnchorStyles.Right; chooseWeek.Click += (s, e) => OpenWeekLookup(); toolbar.Controls.Add(chooseWeek);
            body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Margin = new Padding(0), Padding = new Padding(0, 0, 2, 0) }; layout.Controls.Add(body, 0, 3);
            body.SizeChanged += (s, e) => ResizeBody();
            extraPageHost = new Panel { Dock = DockStyle.Fill, Visible = false }; center.Controls.Add(extraPageHost);
            focusPage = new FocusPage(store); statisticsPage = new StatisticsPage(store);
            deadlinePage = new DeadlinePage(store); deadlinePage.Changed = () => UpdateWallet(); extraPageHost.Controls.Add(deadlinePage);
            focusPage.Changed = () => { UpdateWallet(); }; extraPageHost.Controls.Add(focusPage); extraPageHost.Controls.Add(statisticsPage);
            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 24, 16, 18), Margin = new Padding(0) }; root.Controls.Add(right, 2, 0); rightRail = right;
            var rightFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true }; right.Controls.Add(rightFlow);
            var wallet = new Card { Size = new Size(278, 232), Margin = new Padding(0, 0, 0, 14) }; rightFlow.Controls.Add(wallet);
            var wl = Theme.Label("VÍ THỜI GIAN", 10, Theme.Purple, true); wl.SetBounds(20, 17, 238, 30); wallet.Controls.Add(wl);
            timerState = new PillLabel {Text="Sẵn sàng nghỉ ngơi",Font=Theme.Font(8.5f,true),ForeColor=Theme.Green,BackColor=Theme.Tint(Theme.Green,.08),TextAlign=ContentAlignment.MiddleCenter}; timerState.SetBounds(20, 45, 238, 25); wallet.Controls.Add(timerState);
            balance = Theme.Label("00:00:00", 31, Theme.Ink, true); balance.Font = new Font("Consolas", 31, FontStyle.Bold); balance.SetBounds(16, 70, 250, 51); wallet.Controls.Add(balance);
            var unit = Theme.Label("GIỜ  :  PHÚT  :  GIÂY", 8, Theme.Muted, true); unit.SetBounds(23, 120, 230, 22); wallet.Controls.Add(unit);
            play = Theme.Button("Bắt đầu giải trí", true); play.SetBounds(20, 151, 238, 40); play.Click += (s, e) => Act(() => { if (store.Data.Running) store.Pause(); else store.Start(); expiryShown = false; }, false); wallet.Controls.Add(play);
            walletNote = Theme.Label("", 8, Theme.Muted); walletNote.SetBounds(20, 195, 238, 32); wallet.Controls.Add(walletNote);
            monthlyGoals = new MonthlyGoalsCard(); rightFlow.Controls.Add(monthlyGoals);
            monthlyGoals.MonthChanged += month => { goalsMonth = month; UpdateGoals(); };
            monthlyGoals.CompletionRequested += (id, done) => BeginInvoke((Action)(() => Act(() => store.SetGoalDone(id, done))));
            monthlyGoals.EditRequested += OpenGoals;
            monthlyGoals.CollapseRequested += () => { statsGoalsOpen = !statsGoalsOpen; ApplyRightRail(); };
            goalsToggle = monthlyGoals.CollapseButton; goalsEdit = monthlyGoals.EditButton;
            ticker.Interval = 250; ticker.Tick += Tick; ticker.Start();
            FormClosing += ClosingApp; FormClosed += (s, e) => { ticker.Dispose(); tips.Dispose(); }; Shown += (s, e) => { Render(); if (store.Recovered) MessageBox.Show(this, "Tệp dữ liệu chính gặp lỗi. App đã khôi phục bản sao lưu gần nhất; một số thay đổi cuối có thể chưa có. Tệp lỗi được giữ lại trong thư mục data.", "Đã khôi phục dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning); };
            Render();
        }
        static string Key(DateTime d) { return d.ToString("yyyy-MM-dd"); }
        static DateTime Monday(DateTime d) { return d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }
        Button Nav(string text, int top, Panel side) {
            var b = Theme.Button(text); b.SetBounds(12, top, 156, 46); b.Font = Theme.Font(9, true); b.TextAlign = ContentAlignment.MiddleLeft; b.Padding = new Padding(35, 0, 0, 0); b.FlatAppearance.BorderSize = 0; b.BackColor = Theme.Sidebar; b.ForeColor = Theme.Ink;
            string glyph = text == "Hôm nay" ? "\uE80F" : text == "Lịch tuần" ? "\uE787" : text == "Deadline" ? "\uE823" : text == "Tra cứu tuần" ? "\uE721" : text == "Bấm giờ" ? "\uE916" : text == "Thống kê" ? "\uE9D9" : "\uE713";
            ((RoundedButton)b).NavigationGlyph=glyph;
            side.Controls.Add(b); return b;
        }
        void SelectNav(Button button, bool selected) { button.BackColor = selected ? Color.FromArgb(232,225,255) : Theme.Sidebar; button.ForeColor = selected ? Theme.Purple : Theme.Ink; button.Tag=selected; button.Invalidate(); }
        PlanningProgress AddProgress(Control card) {
            var progress = new PlanningProgress(); card.Controls.Add(progress);
            Action fit = () => progress.SetBounds(14, card.Height - 23, Math.Max(20, card.Width - 28), 16);
            card.Resize += (s, e) => fit(); fit(); return progress;
        }
        Label Stat(TableLayoutPanel parent, int col, string caption, Color color) {
            var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(col == 0 ? 0 : 4, 0, col == 2 ? 0 : 4, 0) }; parent.Controls.Add(card, col, 0);
            var label = Theme.Label(caption, 7.5f, Theme.Muted, true); label.SetBounds(14, 6, 150, 23); label.AutoEllipsis = true; card.Controls.Add(label);
            var value = Theme.Label("", 14, color, true); value.SetBounds(14, 29, 150, 31); value.AutoEllipsis = true; card.Controls.Add(value);
            card.Resize += (s, e) => { label.Width = Math.Max(20, card.ClientSize.Width - 28); value.Width = Math.Max(20, card.ClientSize.Width - 28); };
            label.Width = value.Width = Math.Max(20, card.ClientSize.Width - 28); return value;
        }
        void Act(Action action, bool redraw = true) {
            try { action(); if (redraw) Render(); else UpdateWallet(); }
            catch (Exception ex) { MessageBox.Show(this, "Không thể lưu thay đổi.\n" + ex.Message, "Nhịp", MessageBoxButtons.OK, MessageBoxIcon.Warning); Render(); }
        }
        void EditTask(TaskItem t, DateTime selected, bool preciseTime = false) {
            using (var dialog = new TaskDialog(t, selected, store.Data.Settings.DefaultReward, preciseTime, false, store,t==null && activePage==0 && weekView)) if (dialog.ShowDialog(this) == DialogResult.OK) Act(() => {if(dialog.UsesDeadlineSource)store.ScheduleTaskFromDeadline(dialog.SelectedDeadlineId,dialog.Result.Id,dialog.Result.Day,dialog.Result.Time,dialog.Result.Duration,dialog.RescheduleConfirmed);else store.SaveTaskWithDeadline(dialog.Result, dialog.SelectedDeadlineId);});
        }
        void CopyTask(TaskItem t) {
            using (var dialog = new TaskDialog(Store.CopyDraft(t), DateTime.Today, store.Data.Settings.DefaultReward, false, true, store)) {
                if (dialog.ShowDialog(this) == DialogResult.OK) Act(() => store.SaveTaskWithDeadline(dialog.Result, dialog.SelectedDeadlineId));
            }
        }
        void ChangeTaskColor(TaskItem t) { using (var dialog = new TaskColorDialog(t.ColorHex)) if (dialog.ShowDialog(this) == DialogResult.OK) Act(() => store.SetTaskColor(t.Id, dialog.ColorHex)); }
        ContextMenuStrip TaskMenu(TaskItem t) {
            var menu = new ContextMenuStrip(); menu.Items.Add("Sửa công việc", null, (s, e) => EditTask(t, DateTime.Today));
            menu.Items.Add("Sao chép…", null, (s, e) => CopyTask(t)); menu.Items.Add("Đổi màu…", null, (s, e) => ChangeTaskColor(t));
            menu.Items.Add("Xóa công việc", null, (s, e) => DeleteTask(t)); return menu;
        }
        void OpenSettings() {
            using (var dialog = new SettingsDialog(store.Data)) if (dialog.ShowDialog(this) == DialogResult.OK) Act(() => store.SaveSettings(dialog.DefaultReward, dialog.Rewards));
        }
        void OpenWeekLookup() { using (var dialog = new WeekLookupDialog(store.Data, weekStart)) if (dialog.ShowDialog(this) == DialogResult.OK) { weekStart = dialog.SelectedWeek; weekView = true; activePage = 0; Render(); } }
        void OpenGoals() {
            using (var dialog = new GoalsDialog(store.Data, goalsMonth)) if (dialog.ShowDialog(this) == DialogResult.OK) Act(() => store.SaveGoals(goalsMonth.ToString("yyyy-MM"), dialog.Goals));
        }
        void UpdateGoals() {
            monthlyGoals.Bind(goalsMonth, store.Data.Goals.Where(g => g.Month == goalsMonth.ToString("yyyy-MM")));
            UpdatePlanningSummary();
        }
        void UpdatePlanningSummary() {
            var tasks = store.Data.Tasks.Where(t => weekView ? String.CompareOrdinal(t.Day, Key(weekStart)) >= 0 && String.CompareOrdinal(t.Day, Key(weekStart.AddDays(7))) < 0 : t.Day == Key(DateTime.Today)).ToList();
            completeCaption.Text = weekView ? "CÔNG VIỆC TRONG TUẦN" : "CÔNG VIỆC HÔM NAY";
            earnedCaption.Text = weekView ? "PHẦN THƯỞNG TRONG TUẦN" : "PHẦN THƯỞNG HÔM NAY";
            complete.Text = tasks.Count(t => t.Done) + " / " + tasks.Count + " hoàn thành";
            earned.Text = "+" + tasks.Where(t => t.Done).Sum(t => t.Awarded) + " phút";
            var goals = store.Data.Goals.Where(g => g.Month == goalsMonth.ToString("yyyy-MM")).ToList();
            goalSummary.Text = goals.Count(Store.GoalIsDone) + " / " + goals.Count + " mục tiêu";
            goalSummary.Parent.Controls.OfType<Label>().First(l => l != goalSummary).Text = "MỤC TIÊU THÁNG " + goalsMonth.ToString("MM/yyyy");
            todayProgress.Completed = tasks.Count(t => t.Done); todayProgress.Count = tasks.Count; todayProgress.Invalidate();
            monthProgress.Completed = goals.Count(Store.GoalIsDone); monthProgress.Count = goals.Count; monthProgress.Invalidate();
        }
        void ApplyRightRail() {
            if (monthlyGoals == null) return;
            bool compact = activePage == 2;
            monthlyGoals.SetExpanded(!compact || statsGoalsOpen, compact);
        }
        void ToggleTask(TaskItem t, bool done) {
            if (!done && store.CurrentBalance() < t.Awarded * 60 && MessageBox.Show(this, "Bỏ tick sẽ thu hồi " + t.Awarded + " phút. Phần đã sử dụng sẽ được bù bằng lần thưởng tiếp theo. Tiếp tục?", "Thu hồi phần thưởng", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { BeginInvoke((Action)Render); return; }
            BeginInvoke((Action)(() => Act(() => store.SetDone(t.Id, done))));
        }
        void DeleteTask(TaskItem t) {
            string message = "Xóa công việc “" + t.Title + "”?" + (t.Done ? "\nApp sẽ thu hồi " + t.Awarded + " phút đã thưởng. Phần đã dùng sẽ được bù bằng lần thưởng sau." : "");
            if (MessageBox.Show(this, message, "Xóa công việc", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Act(() => store.Delete(t.Id));
        }
        void ClearBody() { if (calendar != null && !calendar.IsDisposed) calendarViewMinute = calendar.ViewMinute; calendar = null; foreach (Control c in body.Controls.Cast<Control>().ToArray()) c.Dispose(); }
        bool ConfirmDeadlineSchedule(string id, DateTime end) {
            var d = store.ParentDeadline(id);
            return d == null || end <= d.Due || MessageBox.Show(this, "Lịch này kết thúc sau hạn deadline “" + d.Title + "”. Vẫn lưu lịch?", "Lịch sau hạn", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }
        public void WriteDeadlinePreview(string directory) {
            Directory.CreateDirectory(directory); activePage = 3; deadlinePage.ShowOverview(0); Render(); PerformLayout(); Refresh();
            SaveScreen(directory, "deadline-tong-quan.png");
            var full = Size; Size = MinimumSize; PerformLayout(); Refresh(); SaveScreen(directory,"deadline-tong-quan-nho.png"); Size = full;
            if (store.Data.Deadlines.Count > 0) { deadlinePage.OpenDetail(store.Data.Deadlines[0].Id); PerformLayout(); Refresh(); SaveScreen(directory,"deadline-chi-tiet.png"); Size = MinimumSize; PerformLayout(); Refresh(); SaveScreen(directory,"deadline-chi-tiet-nho.png"); File.WriteAllLines(Path.Combine(directory,"layout.txt"),Descendants(deadlinePage).Select(c => c.GetType().Name+" "+c.Text.Replace("\n"," ")+" "+c.Bounds+" parent="+c.Parent.ClientSize+""+(c is ScrollableControl ? " scroll="+((ScrollableControl)c).AutoScrollMinSize+" display="+((ScrollableControl)c).DisplayRectangle : ""))); Size = full; }
            using (var dialog = new DeadlineDialog(null)) { dialog.Show(); dialog.Refresh(); using(var bitmap = new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(Path.Combine(directory,"tao-deadline.png"));} dialog.Close(); }
        }
        void SaveScreen(string directory,string filename) { using(var bitmap = new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(directory,filename));} }
        void Render() {
            if (IsDisposed) return;
            rightRail.Visible = activePage != 3; root.ColumnStyles[2].Width = activePage == 3 ? 0 : 312;
            SelectNav(deadlineButton, activePage == 3); deadlinePage.Visible = activePage == 3;
            planningLayout.Visible = activePage == 0; extraPageHost.Visible = activePage != 0;
            SelectNav(todayButton, activePage == 0 && !weekView); SelectNav(weekButton, activePage == 0 && weekView);
            SelectNav(focusButton, activePage == 1); SelectNav(statisticsButton, activePage == 2);
            if (activePage != 0) {
                extraPageHost.BringToFront(); focusPage.Visible = activePage == 1; statisticsPage.Visible = activePage == 2;
                if (activePage == 1) { focusPage.BringToFront(); focusPage.RefreshState(); } else if (activePage == 2) { statisticsPage.BringToFront(); statisticsPage.RefreshData(); } else { deadlinePage.BringToFront(); deadlinePage.RefreshData(); }
                UpdateWallet(); UpdateGoals(); ApplyRightRail(); return;
            }
            planningLayout.BringToFront();
            planningToolbar.Visible = weekView;
            planningLayout.RowStyles[1].Height = weekView ? 78 : 100;
            planningLayout.RowStyles[2].Height = weekView ? 46 : 0;
            todayProgress.Visible = monthProgress.Visible = !weekView;
            body.AutoScroll = !weekView;
            body.SuspendLayout(); ClearBody();
            title.Text = weekView ? "Lịch tuần" : "Hôm nay của bạn";
            title.Font = Theme.Font(26, true); title.Width = Math.Max(180, title.Parent.ClientSize.Width - 152);
            subtitle.Text = weekView ? weekStart.ToString("dd/MM") + " — " + weekStart.AddDays(6).ToString("dd/MM/yyyy") : DateTime.Today.ToString("dddd, dd 'tháng' MM, yyyy", CultureInfo.GetCultureInfo("vi-VN"));
            motivation.Text = WeeklyMotivation.ForWeek(weekStart); tips.SetToolTip(motivation, motivation.Text);
            if (!weekView) selectedDay = DateTime.Today;
            else if (selectedDay < weekStart || selectedDay >= weekStart.AddDays(7)) selectedDay = DateTime.Today >= weekStart && DateTime.Today < weekStart.AddDays(7) ? DateTime.Today : weekStart;
            if (weekView) RenderWeek(); else RenderToday();
            body.ResumeLayout(); ResizeBody(); UpdateWallet(); UpdateGoals(); ApplyRightRail();
        }
        void RenderToday() {
            var overdue = store.Data.Tasks.Where(t => String.CompareOrdinal(t.Day, Key(DateTime.Today)) < 0 && !t.Done).OrderBy(t => t.Day).ThenBy(t => t.Time).ToList();
            var todays = store.Data.Tasks.Where(t => t.Day == Key(DateTime.Today)).OrderBy(t => t.Time).ToList();
            if (overdue.Count + todays.Count == 0) {
                var empty = new Card { Height = 255, Margin = new Padding(0, 0, 0, 12) };
                var a = Theme.Label("Một ngày mới, một khởi đầu mới.", 19, Theme.Ink, true); a.Dock = DockStyle.Top; a.Height = 76; empty.Controls.Add(a);
                var b = Theme.Label("Thêm công việc đầu tiên và chọn số phút giải trí\nbạn muốn tự thưởng sau khi hoàn thành.", 11, Theme.Muted); b.SetBounds(16, 98, 650, 62); b.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top; empty.Controls.Add(b);
                var add = Theme.Button("+ Tạo việc đầu tiên", true); add.SetBounds(16, 176, 180, 42); add.Click += (s, e) => EditTask(null, DateTime.Today); empty.Controls.Add(add); body.Controls.Add(empty); return;
            }
            if (overdue.Count > 0) {
                var banner = new Card { Width = 600, Height = 100, Margin = new Padding(0, 0, 0, 12), BackColor = Color.FromArgb(255, 248, 231) };
                var heading = Theme.Label("CÒN CHƯA XONG TỪ NGÀY TRƯỚC · " + overdue.Count + " VIỆC", 9, Color.FromArgb(167, 104, 28), true); banner.Controls.Add(heading);
                var description = Theme.Label(overdue[0].Title + (overdue.Count > 1 ? "  ·  và " + (overdue.Count - 1) + " việc khác" : ""), 10, Theme.Ink, true); description.AutoEllipsis = true; banner.Controls.Add(description); tips.SetToolTip(description, String.Join("\n", overdue.Select(t => t.Title)));
                var meta = Theme.Label(DateTime.ParseExact(overdue[0].Day, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd/MM") + " · " + overdue[0].Time + " · " + overdue[0].Duration + " phút", 9, Theme.Muted); banner.Controls.Add(meta);
                var expand = Theme.Button(overdueExpanded ? "Thu gọn" : "Xem việc"); expand.Name = "OverdueToggle"; expand.Click += (s, e) => { overdueExpanded = !overdueExpanded; Render(); }; banner.Controls.Add(expand);
                Action fit = () => { int w = banner.ClientSize.Width; heading.SetBounds(16, 8, w - 32, 24); description.SetBounds(16, 35, Math.Max(20, w - 150), 28); meta.SetBounds(16, 66, w - 150, 22); expand.SetBounds(w - 120, 39, 104, 36); };
                banner.Resize += (s, e) => fit(); fit(); body.Controls.Add(banner);
                if (overdueExpanded) foreach (var t in overdue) { var card = TaskCard(t, false, true); card.Tag = "overdue:" + t.Id; body.Controls.Add(card); }
            }
            var section = new Panel { Height = 35, Margin = new Padding(0) };
            var count = Theme.Label("HÔM NAY · " + todays.Count + " VIỆC", 9, Theme.Muted, true); section.Controls.Add(count);
            var order = Theme.Label("Theo giờ bắt đầu", 9, Theme.Muted); order.TextAlign = ContentAlignment.MiddleRight; section.Controls.Add(order);
            section.Resize += (s, e) => { count.SetBounds(0, 0, Math.Max(20, section.Width - 150), 30); order.SetBounds(section.Width - 150, 0, 148, 30); }; body.Controls.Add(section);
            for (int i = 0; i < todays.Count; i++) body.Controls.Add(new TimelineEntry(todays[i], TaskCard(todays[i], false, false)) { First = i == 0, Last = i == todays.Count - 1 });
            if (todays.Count == 0) { var empty = Theme.Label("Hôm nay chưa có công việc. Bấm + Thêm việc để lên lịch.", 11, Theme.Muted); empty.Height = 90; body.Controls.Add(empty); }
        }
        void RenderWeek() {
            calendar = new WeekCalendar(weekStart) { Margin = new Padding(0), Size = new Size(900, Math.Max(280, body.ClientSize.Height - 4)) };
            calendar.DeadlineLabel = id => { var p = store.ParentDeadline(id); return p == null ? null : p.Title; };
            calendar.SetTasks(store.Data.Tasks); calendar.ViewMinute = calendarViewMinute;
            calendar.SelectedDay = selectedDay;
            calendar.DaySelected += day => { selectedDay = day; ApplyRightRail(); };
            calendar.ScheduleChanged += change => {
                if (!ConfirmDeadlineSchedule(change.Id, change.Start.AddMinutes(change.Duration))) { calendar.SetTasks(store.Data.Tasks); return; }
                Act(() => store.Reschedule(change.Id, Key(change.Start), change.Start.ToString("HH:mm"), change.Duration), false);
                if (calendar != null && !calendar.IsDisposed) calendar.SetTasks(store.Data.Tasks);
                ApplyRightRail(); UpdatePlanningSummary();
            };
            calendar.EditRequested += t => EditTask(t, DateTime.Today);
            calendar.ToggleRequested += t => ToggleTask(t, !t.Done);
            calendar.DeleteRequested += DeleteTask;
            calendar.CopyRequested += CopyTask; calendar.ColorRequested += ChangeTaskColor;
            calendar.CreateRequested += date => EditTask(null, date, true);
            body.Controls.Add(calendar);
        }
        Control TaskCard(TaskItem t, bool compact, bool overdue) {
            var card = new Card { Width = compact ? 110 : 600, Height = compact ? 204 : 88, Margin = new Padding(0, 0, 0, compact ? 7 : 10), Padding = new Padding(compact ? 0 : 4), BackColor = Theme.Tint(Theme.TaskColor(t), t.Done ? .035 : .075) };
            var taskMenu = TaskMenu(t); card.ContextMenuStrip = taskMenu; card.Disposed += (s, e) => UiMenus.DisposeAfterEvent(taskMenu);
            if (compact) {
                var check = new CheckBox { Text = t.Time, Checked = t.Done, Font = Theme.Font(9, true), ForeColor = Theme.Ink, AutoSize = false, Cursor = Cursors.Hand }; check.SetBounds(8, 8, 94, 26); check.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top; check.CheckedChanged += (s, e) => ToggleTask(t, check.Checked); card.Controls.Add(check);
                var name = Theme.Label(t.Title, 9, t.Done ? Theme.Muted : Theme.Ink, true); name.SetBounds(8, 38, 94, 75); name.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right; name.AutoEllipsis = true; name.TextAlign = ContentAlignment.TopLeft; card.Controls.Add(name); tips.SetToolTip(name, t.Title + "\n" + t.Time + " · " + t.Duration + " phút · " + t.Category); name.DoubleClick += (s, e) => EditTask(t, DateTime.Today);
                var reward = Theme.Label("+" + (t.Done ? t.Awarded : t.Reward) + " phút", 9, Theme.Green, true); reward.SetBounds(8, 116, 94, 24); reward.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top; card.Controls.Add(reward);
                var cat = Theme.Label(t.Duration + "p · " + t.Category, 8, Theme.CategoryColor(t.Category)); cat.SetBounds(8, 141, 94, 23); cat.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top; cat.AutoEllipsis = true; card.Controls.Add(cat);
                var edit = Theme.Button("Sửa"); edit.Font = Theme.Font(8); edit.SetBounds(6, 169, 44, 27); edit.Click += (s, e) => EditTask(t, DateTime.Today); card.Controls.Add(edit);
                var del = Theme.Button("×"); del.Font = Theme.Font(11); del.SetBounds(55, 169, 30, 27); del.Click += (s, e) => DeleteTask(t); card.Controls.Add(del);
            } else {
                var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Padding = new Padding(8), Margin = new Padding(0) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 51)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 37));
                row.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); card.Controls.Add(row);
                var check = new CheckBox { Checked = t.Done, Dock = DockStyle.Fill, Cursor = Cursors.Hand }; check.CheckedChanged += (s, e) => ToggleTask(t, check.Checked); row.Controls.Add(check, 0, 0);
                var details = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }; row.Controls.Add(details, 1, 0);
                var parentDeadline = store.ParentDeadline(t.Id);
                var name = Theme.Label((parentDeadline != null ? "[Deadline] " : "") + t.Title, 10.5f, t.Done ? Theme.Muted : Theme.Ink, true); name.Dock = DockStyle.Top; name.Height = 35; name.AutoEllipsis = true; details.Controls.Add(name); tips.SetToolTip(name, t.Title + (parentDeadline == null ? "" : "\nDeadline: " + parentDeadline.Title)); name.DoubleClick += (s, e) => EditTask(t, DateTime.Today);
                DateTime start = DateTime.ParseExact(t.Day + " " + t.Time, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), end = start.AddMinutes(t.Duration);
                string timeRange = t.Time + "–" + end.ToString("HH:mm") + (end.Date > start.Date ? " (+1 ngày)" : "");
                var meta = Theme.Label((overdue ? start.ToString("dd/MM") + " · " : "") + timeRange + "  ·  " + t.Duration + " phút  ·  " + t.Category, 8.5f, Theme.CategoryColor(t.Category)); meta.Dock = DockStyle.Bottom; meta.Height = 24; meta.AutoEllipsis = true; details.Controls.Add(meta); tips.SetToolTip(meta, start.ToString("dd/MM HH:mm") + " — " + end.ToString("dd/MM HH:mm") + " · " + t.Duration + " phút · " + t.Category);
                var reward = Theme.Label("+" + (t.Done ? t.Awarded : t.Reward) + " phút", 10, Theme.Green, true); reward.Anchor = AnchorStyles.None; reward.Size = new Size(84, 30); reward.BackColor = Theme.Tint(Theme.Green, .09); reward.TextAlign = ContentAlignment.MiddleCenter; row.Controls.Add(reward, 2, 0);
                var edit = Theme.Button("Sửa"); edit.Font = Theme.Font(9); edit.Dock = DockStyle.Fill; edit.Margin = new Padding(2, 14, 2, 14); edit.Click += (s, e) => EditTask(t, DateTime.Today); row.Controls.Add(edit, 3, 0);
                var del = Theme.Button("⋯"); del.Dock = DockStyle.Fill; del.Margin = new Padding(3, 14, 0, 14); del.Click += (s, e) => taskMenu.Show(del, new Point(0, del.Height)); row.Controls.Add(del, 4, 0);
            }
            return card;
        }
        void ResizeBody() {
            if (resizingBody) return; resizingBody = true;
            try {
                int scrollY = -body.AutoScrollPosition.Y; bool weekly = weekView;
                body.AutoScroll = false; body.AutoScrollMinSize = Size.Empty;
                int width = Math.Max(100, body.ClientSize.Width - (weekly ? 0 : SystemInformation.VerticalScrollBarWidth) - 8);
                foreach (Control c in body.Controls) {
                    c.Width = c is WeekCalendar ? Math.Max(500, width) : width;
                    if (c is WeekCalendar) c.Height = Math.Max(280, body.ClientSize.Height - (width < 500 ? 24 : 4));
                }
                body.PerformLayout(); body.AutoScroll = !weekly; body.PerformLayout(); body.AutoScrollPosition = new Point(0, scrollY);
            } finally { resizingBody = false; }
        }
        void UpdateWallet() {
            double seconds = store.CurrentBalance(); long total = (long)Math.Ceiling(Math.Abs(seconds));
            balance.Text = (seconds < 0 ? "−" : "") + (total / 3600).ToString("00") + ":" + (total / 60 % 60).ToString("00") + ":" + (total % 60).ToString("00");
            float fontSize = total >= 360000 || seconds < 0 ? 26 : 31;
            if (balance.Font.Size != fontSize) { var previousFont = balance.Font; balance.Font = new Font("Consolas", fontSize, FontStyle.Bold); previousFont.Dispose(); }
            timerState.Text = store.Data.Running ? "● Đang dùng thời gian giải trí" : seconds < 0 ? "Cần bù thời gian đã sử dụng" : "Sẵn sàng nghỉ ngơi";
            timerState.ForeColor = seconds < 0 ? Color.FromArgb(179,72,89) : store.Data.Running ? Theme.Purple : Theme.Green;
            timerState.BackColor = Theme.Tint(timerState.ForeColor,.08);
            play.Text = store.Data.Running ? "Tạm dừng" : "Bắt đầu giải trí";
            play.Enabled = store.Data.Running || seconds > 0;
            walletNote.Text = seconds < 0 ? "Phần thưởng tiếp theo sẽ bù số phút âm trước khi bạn có thể giải trí tiếp." : "Chỉ giảm khi bạn bấm bắt đầu.\nĐóng app sẽ tự tạm dừng.";
        }
        void Tick(object sender, EventArgs e) {
            if (activePage == 3 && Enabled) deadlinePage.RefreshIfStatusChanged();
            if (store.Data.ActiveFocus != null && store.Data.ActiveFocus.Running && (DateTime.UtcNow.Ticks - store.Data.ActiveFocus.Anchor) >= TimeSpan.FromSeconds(15).Ticks) {
                try { store.CheckpointFocus(); } catch (Exception ex) { ticker.Stop(); MessageBox.Show(this, "Chưa lưu được mốc bấm giờ: " + ex.Message + "\nHãy thử tạm dừng và lưu phiên.", "Lỗi lưu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
            if (lastDay != Key(DateTime.Today)) { lastDay = Key(DateTime.Today); Render(); }
            if (store.Data.Running && store.CurrentBalance() <= 0 && !expiryShown) {
                expiryShown = true; Act(() => store.Expire(), false); System.Media.SystemSounds.Exclamation.Play();
                MessageBox.Show(this, "Bạn đã dùng hết thời gian giải trí. Hãy quay lại công việc để tích lũy thêm nhé!", "Đã hết giờ giải trí", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            UpdateWallet();
        }
        void ClosingApp(object sender, FormClosingEventArgs e) {
            try { if (store.Data.ActiveFocus != null && store.Data.ActiveFocus.Running) store.PauseFocus(); if (store.Data.Running) store.Pause(); ticker.Stop(); }
            catch (Exception ex) { e.Cancel = true; MessageBox.Show(this, "Chưa lưu được bộ đếm nên app vẫn đang mở.\n" + ex.Message, "Không thể lưu", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        public void WritePreview(string directory) {
            activePage = 0; weekView = false; Render(); PerformLayout();
            Refresh();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "hom-nay.png")); }
            using (var bitmap = new Bitmap(monthlyGoals.Width, monthlyGoals.Height)) { monthlyGoals.DrawToBitmap(bitmap, new Rectangle(0, 0, monthlyGoals.Width, monthlyGoals.Height)); bitmap.Save(Path.Combine(directory, "muc-tieu-thang.png")); }
            var todayFullSize = Size; Size = MinimumSize; PerformLayout(); ResizeBody(); Refresh();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "hom-nay-nho.png")); }
            Size = todayFullSize; PerformLayout(); ResizeBody();
            weekView = true; Render(); PerformLayout();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "lich-tuan.png")); }
            var fullSize = Size; Size = MinimumSize; PerformLayout(); ResizeBody();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "lich-tuan-nho.png")); }
            Size = fullSize; PerformLayout(); ResizeBody();
            using (var dialog = new TaskDialog(null, DateTime.Today, store.Data.Settings.DefaultReward, false, false, store)) {
                dialog.Show(); dialog.Refresh();
                using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(0, 0, dialog.Width, dialog.Height)); bitmap.Save(Path.Combine(directory, "them-viec.png")); }
                dialog.Close();
            }
            using (var dialog = new SettingsDialog(store.Data)) {
                dialog.Show(); dialog.Refresh();
                using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(0, 0, dialog.Width, dialog.Height)); bitmap.Save(Path.Combine(directory, "cai-dat.png")); }
                dialog.Close();
            }
            using (var dialog = new GoalsDialog(store.Data, goalsMonth)) {
                dialog.Show(); dialog.Refresh();
                using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(0, 0, dialog.Width, dialog.Height)); bitmap.Save(Path.Combine(directory, "muc-tieu.png")); }
                dialog.Close();
            }
            activePage = 1; Render(); PerformLayout();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "bam-gio.png")); }
            Size = MinimumSize; PerformLayout(); focusPage.PerformLayout();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "bam-gio-nho.png")); }
            Size = fullSize; PerformLayout();
            activePage = 2; Render(); PerformLayout();
            Refresh();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "thong-ke.png")); }
            Size = MinimumSize; PerformLayout(); statisticsPage.PerformLayout(); Refresh();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "thong-ke-nho.png")); }
            Size = fullSize; PerformLayout(); statisticsPage.PresetForTest("12 tháng"); Refresh();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "thong-ke-12-thang.png")); }
            statisticsPage.PresetForTest("7 ngày");
            activePage = 0; Render();
        }
        static System.Collections.Generic.IEnumerable<Control> Descendants(Control root) {
            foreach (Control c in root.Controls) { yield return c; foreach (Control child in Descendants(c)) yield return child; }
        }
        void RunDeadlineUiTests(Action<bool,string> check) {
            deadlineButton.PerformClick(); Application.DoEvents();
            check(activePage == 3 && deadlinePage.Visible && !rightRail.Visible && root.ColumnStyles[2].Width == 0, "deadline navigation uses full content width");
            check(deadlinePage.VisibleDeadlines == 0, "new deadline page starts empty");
            using (var modal = new System.Windows.Forms.Timer { Interval=80 }) {
                modal.Tick += (s,e) => {
                    var d=Application.OpenForms.OfType<DeadlineDialog>().FirstOrDefault();
                    if(d!=null){modal.Stop();d.FillForTest("Đồ án kiểm tra giao diện");d.AcceptButton.PerformClick();return;}
                    var t=Application.OpenForms.OfType<DeadlineTaskDialog>().FirstOrDefault();
                    if(t!=null){modal.Stop();t.FillForTest("Việc nhỏ kiểm tra");t.AcceptButton.PerformClick();return;}
                    var schedule=Application.OpenForms.OfType<TaskDialog>().FirstOrDefault();
                    if(schedule!=null){modal.Stop();schedule.FillForTest("Việc nhỏ kiểm tra",15);return;}
                    var link=Application.OpenForms.OfType<AttachTaskDialog>().FirstOrDefault();
                    if(link!=null){modal.Stop();link.AcceptButton.PerformClick();}
                };
                modal.Start(); Descendants(deadlinePage).OfType<Button>().First(b=>b.Text=="+ Tạo deadline").PerformClick(); modal.Stop();
                string id=store.Data.Deadlines[0].Id;
                check(store.Data.Deadlines.Count==1 && deadlinePage.SelectedId==id, "create deadline dialog opens new detail screen");
                check(!deadlinePage.CompletionEnabled, "empty deadline disables final confirmation");
                int calendarCount=store.Data.Tasks.Count;
                modal.Start();Descendants(deadlinePage).OfType<Button>().First(b=>b.Text=="+ Thêm việc").PerformClick();modal.Stop();
                var child=store.Data.UnscheduledTasks.Single();
                check(store.Data.Tasks.Count==calendarCount && store.ParentDeadline(child.Id).Id==id, "add child dialog keeps unscheduled work off calendar");
                double balanceBefore=store.Data.Balance;int expectedReward=child.Reward;
                Descendants(deadlinePage).OfType<CheckBox>().Single().Checked=true;Application.DoEvents();
                check(store.AllTasks.First(t=>t.Id==child.Id).Done && store.Data.Balance==balanceBefore+expectedReward*60 && deadlinePage.CompletionEnabled, "child checkbox awards once and enables handover");
                Descendants(deadlinePage).OfType<Button>().First(b=>b.Text.Contains("Đã nộp / bàn giao")).PerformClick();
                check(store.Data.Deadlines[0].Completed && store.Data.Balance==balanceBefore+expectedReward*60, "handover confirmation completes parent without reward");
                deadlinePage.ShowOverview(2);check(deadlinePage.VisibleDeadlines==1, "completed deadline appears in completed tab");
                deadlinePage.OpenDetail(id);Descendants(deadlinePage).OfType<Button>().First(b=>b.Text=="Mở lại deadline").PerformClick();
                check(!store.Data.Deadlines[0].Completed,"reopen button restores active deadline");
                modal.Start();Descendants(deadlinePage).OfType<Button>().First(b=>b.Text=="Đưa vào lịch").PerformClick();modal.Stop();
                check(store.IsScheduled(child.Id) && store.Data.Tasks.Count==calendarCount+1 && store.Data.UnscheduledTasks.Count==0, "schedule dialog moves pending task into shared calendar");
                modal.Start();Descendants(deadlinePage).OfType<Button>().First(b=>b.Text=="Gắn việc có sẵn").PerformClick();modal.Stop();
                check(store.Data.Deadlines[0].TaskIds.Count==2,"attach existing task dialog preserves original task");
                var size=Size;Size=MinimumSize;PerformLayout();Application.DoEvents();
                check(!Descendants(deadlinePage).OfType<FlowLayoutPanel>().First().HorizontalScroll.Visible,"deadline detail has no horizontal scroll at minimum size");
                check(Descendants(deadlinePage).OfType<Button>().Where(b=>b.Visible).All(b=>b.Left>=0 && b.Right<=b.Parent.ClientSize.Width),"deadline detail buttons fit minimum window");
                Size=size;PerformLayout();
                weekButton.PerformClick();check(rightRail.Visible && root.ColumnStyles[2].Width==312 && calendar!=null,"returning to calendar restores wallet and goals rail");
                todayButton.PerformClick();
                check(Descendants(body).OfType<Label>().Any(l=>l.Text.Contains("[Deadline]")),"today tasks display deadline link label");
            }
        }
        void RunDeadlineMenuUiTests(Action<bool,string> check) {
            string parent=store.Data.Deadlines.First().Id;
            var pending=new TaskItem {Id="menu-pending",Title="Việc kiểm tra menu",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="09:00",Duration=60,Reward=15,Category="Học tập"};store.AddDeadlineTask(parent,pending);
            deadlineButton.PerformClick();deadlinePage.OpenDetail(parent);Application.DoEvents();
            double credits=store.Data.Balance;bool cancel=true;int dialogs=0;
            using(var modal=new System.Windows.Forms.Timer {Interval=70}){
                modal.Tick+=(s,e)=>{
                    var p=Application.OpenForms.OfType<DeadlineTaskDialog>().FirstOrDefault();
                    if(p!=null){modal.Stop();dialogs++;if(cancel)p.CancelButton.PerformClick();else{p.FillForTest("Việc đã sửa qua menu");p.AcceptButton.PerformClick();}return;}
                    var d=Application.OpenForms.OfType<TaskDialog>().FirstOrDefault();if(d!=null){modal.Stop();dialogs++;d.CancelButton.PerformClick();}
                };
                var menu=deadlinePage.OpenTaskMenuForTest(pending.Id);((ToolStripMenuItem)menu.Items[0]).PerformClick();
                check(dialogs==0 && store.AllTasks.First(t=>t.Id==pending.Id).Title=="Việc kiểm tra menu","deadline menu action waits for click processing before opening dialog");
                modal.Start();Application.DoEvents();modal.Stop();Application.DoEvents();
                check(dialogs==1 && menu.IsDisposed && store.Data.UnscheduledTasks.Any(t=>t.Id==pending.Id),"cancel pending edit safely closes and releases menu without deleting work");
                cancel=false;menu=deadlinePage.OpenTaskMenuForTest(pending.Id);((ToolStripMenuItem)menu.Items[0]).PerformClick();modal.Start();Application.DoEvents();modal.Stop();Application.DoEvents();
                check(dialogs==2 && menu.IsDisposed && store.AllTasks.First(t=>t.Id==pending.Id).Title=="Việc đã sửa qua menu" && store.ParentDeadline(pending.Id).Id==parent,"saving pending edit through real menu rebuilds page without disposed menu error");
                string scheduled=store.Data.Deadlines.First(d=>d.Id==parent).TaskIds.First(store.IsScheduled);
                menu=deadlinePage.OpenTaskMenuForTest(scheduled);((ToolStripMenuItem)menu.Items[0]).PerformClick();modal.Start();Application.DoEvents();modal.Stop();Application.DoEvents();
                check(dialogs==3 && menu.IsDisposed && store.IsScheduled(scheduled),"scheduled edit modal also returns safely to deadline menu");
                for(int i=0;i<3;i++){menu=deadlinePage.OpenTaskMenuForTest(pending.Id);menu.Close();Application.DoEvents();check(menu.IsDisposed,"dismiss deadline menu safely on repeat "+(i+1));}
                menu=deadlinePage.OpenTaskMenuForTest(pending.Id);((ToolStripMenuItem)menu.Items[1]).PerformClick();Application.DoEvents();
                check(menu.IsDisposed && store.ParentDeadline(pending.Id)==null && store.Data.UnscheduledTasks.Any(t=>t.Id==pending.Id) && store.Data.Balance==credits,"detach through real menu preserves work and credits without lifecycle error");
                var reopened=new Store(Path.GetDirectoryName(store.FilePath));check(reopened.AllTasks.Any(t=>t.Id==pending.Id && t.Title=="Việc đã sửa qua menu") && reopened.ParentDeadline(pending.Id)==null,"menu edit and detach persist after reopening data");
            }
        }
        void RunTaskDeadlineUiTests(Action<bool,string> check) {
            store.SaveDeadline(new Deadline {Id="edit-link-a",Title="Deadline chọn trong form",DueDay=DateTime.Today.AddDays(20).ToString("yyyy-MM-dd")});
            store.SaveDeadline(new Deadline {Id="edit-link-b",Title="Deadline chuyển sang",DueDay=DateTime.Today.AddDays(21).ToString("yyyy-MM-dd")});
            var task=new TaskItem {Id="edit-link-task",Title="Block gắn trực tiếp",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="09:00",Duration=60,Reward=20,Category="Học tập"};store.Upsert(task);store.SetDone(task.Id,true);
            double before=store.Data.Balance;string choose="edit-link-a";bool cancel=false;string expected=null;
            using(var modal=new System.Windows.Forms.Timer {Interval=80}) {
                modal.Tick+=(s,e)=>{var d=Application.OpenForms.OfType<TaskDialog>().FirstOrDefault();if(d==null)return;modal.Stop();check(d.SelectedDeadlineId==expected,"task editor preselects persisted deadline");d.SelectDeadlineForTest(choose);if(cancel)d.CancelButton.PerformClick();else d.SaveForTest();};
                modal.Start();EditTask(store.Data.Tasks.First(t=>t.Id==task.Id),DateTime.Today);modal.Stop();
                check(store.ParentDeadline(task.Id).Id=="edit-link-a" && store.Data.Balance==before && store.Data.Tasks.First(t=>t.Id==task.Id).Awarded==20,"task editor attaches existing completed block without extra reward");
                expected="edit-link-a";choose="edit-link-b";modal.Start();EditTask(store.Data.Tasks.First(t=>t.Id==task.Id),DateTime.Today);modal.Stop();
                check(store.ParentDeadline(task.Id).Id=="edit-link-b" && store.Data.Deadlines.First(d=>d.Id=="edit-link-a").TaskIds.Count==0,"task editor switches block to another deadline");
                expected="edit-link-b";choose=null;cancel=true;modal.Start();EditTask(store.Data.Tasks.First(t=>t.Id==task.Id),DateTime.Today);modal.Stop();
                check(store.ParentDeadline(task.Id).Id=="edit-link-b" && store.Data.Balance==before,"canceling task editor leaves deadline link unchanged");
                cancel=false;modal.Start();EditTask(store.Data.Tasks.First(t=>t.Id==task.Id),DateTime.Today);modal.Stop();
                check(store.ParentDeadline(task.Id)==null && store.Data.Tasks.First(t=>t.Id==task.Id).Done,"task editor removes link without removing calendar block");
                using(var d=new TaskDialog(store.Data.Tasks.First(t=>t.Id==task.Id),DateTime.Today,15,false,false,store)){
                    check(d.Controls.OfType<ComboBox>().Any(c=>c.DropDownStyle==ComboBoxStyle.DropDownList && c.Items.Count>=3),"task editor offers available deadlines in dropdown");
                    check(d.Height<=Screen.PrimaryScreen.WorkingArea.Height && d.Controls.Cast<Control>().All(c=>c.Bottom<=d.ClientSize.Height),"expanded task editor fits screen and contains save button");
                    d.SelectDeadlineForTest("edit-link-a");d.Show();d.Refresh();using(var bitmap=new Bitmap(d.Width,d.Height)){d.DrawToBitmap(bitmap,new Rectangle(Point.Empty,d.Size));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","task-deadline","chinh-sua-deadline.png"));}d.Close();
                }
            }
        }
        void RunFocusTaskUiTests(Action<bool,string> check) {
            store.Upsert(new TaskItem {Id="focus-pick-open",Title="Hoàn thành báo cáo dự án",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="15:00",Duration=90,Reward=30,Category="Công việc"});
            store.Upsert(new TaskItem {Id="focus-pick-done",Title="Task đã hoàn thành",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="08:00",Duration=60,Reward=0,Category="Học tập"});store.SetDone("focus-pick-done",true);
            store.SaveDeadline(new Deadline {Id="focus-pending-parent",Title="Deadline cho việc chưa xếp lịch",DueDay=DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")});
            store.AddDeadlineTask("focus-pending-parent",new TaskItem {Id="focus-pick-pending",Title="Đọc tài liệu chưa xếp lịch",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="09:00",Duration=60,Reward=15,Category="Học tập"});
            focusButton.PerformClick();
            check(focusPage.OffersTaskForTest("focus-pick-open") && !focusPage.OffersTaskForTest("focus-pick-done") && focusPage.OffersTaskForTest("focus-pick-pending"),"focus picker includes unfinished scheduled and pending tasks and excludes completed ones");
            focusPage.ChooseTaskThroughMenuForTest("focus-pick-pending");check(focusPage.DraftTitleForTest=="Đọc tài liệu chưa xếp lịch" && focusPage.DraftCategoryForTest=="Học tập","pending task selection fills session title and study category");
            focusPage.ChooseTaskForTest("focus-pick-open");check(focusPage.DraftTitleForTest=="Hoàn thành báo cáo dự án" && focusPage.DraftCategoryForTest=="Công việc","scheduled task selection fills session title and work category");
            focusPage.SetDraftForTest("Tập trung vào nội dung riêng");check(focusPage.SelectedTaskForTest==null && focusPage.DraftTitleForTest=="Tập trung vào nội dung riêng","editing session name switches to custom mode without overwriting text");
            focusPage.ChooseTaskForTest("focus-pick-open");focusPage.RefreshState();check(focusPage.SelectedTaskForTest=="focus-pick-open" && focusPage.DraftTitleForTest=="Hoàn thành báo cáo dự án","refresh preserves selected task and session draft");
            double balanceBefore=store.Data.Balance;int sessionsBefore=store.Data.FocusSessions.Count;
            var clock=store.Clock;var now=DateTime.UtcNow;store.Clock=()=>now;
            try {
                focusPage.StartSelectedForTest();check(store.Data.ActiveFocus.Title=="Hoàn thành báo cáo dự án" && store.Data.ActiveFocus.Category=="Công việc" && !focusPage.TaskPickerEnabledForTest,"starting selected task saves chosen title and locks task selection");
                now=now.AddSeconds(45);focusPage.PauseForTest();check(!focusPage.TaskPickerEnabledForTest && store.FocusSeconds()==45,"paused focus keeps task picker locked for the same session");
                focusPage.FinishForTest();check(store.Data.FocusSessions.Count==sessionsBefore+1 && store.Data.FocusSessions.Last().Title=="Hoàn thành báo cáo dự án" && Store.SliceSeconds(store.Data.FocusSessions.Last().Slices)==45,"selected task focus saves exact elapsed time and title");
                check(!store.Data.Tasks.First(t=>t.Id=="focus-pick-open").Done && store.Data.Balance==balanceBefore && focusPage.TaskPickerEnabledForTest,"saving task focus does not complete task or grant reward and unlocks selection");
            } finally {store.Clock=clock;}
            store.SetDone("focus-pick-open",true);focusPage.RefreshState();check(!focusPage.OffersTaskForTest("focus-pick-open") && focusPage.SelectedTaskForTest==null,"task picker removes newly completed task on refresh");
            store.SetDone("focus-pick-open",false);focusPage.RefreshState();focusPage.ChooseTaskForTest("focus-pick-open");
            var size=Size;Size=MinimumSize;PerformLayout();focusPage.PerformLayout();check(focusPage.FitsForTest,"focus task dropdown and history fit minimum window"+(focusPage.FitsForTest?"":" "+focusPage.LayoutForTest));
            Refresh();SaveScreen(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","focus-tasks"),"bam-gio-chon-task-nho.png");Size=size;PerformLayout();Refresh();SaveScreen(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","focus-tasks"),"bam-gio-chon-task.png");
            using(var empty=new FocusPage(new Store(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","focus-empty-"+DateTime.UtcNow.Ticks)))){empty.SetDraftForTest("Tự nhập khi chưa có task");check(empty.SelectedTaskForTest==null && empty.DraftTitleForTest=="Tự nhập khi chưa có task","empty task list still supports custom session title");}
        }
        void RunDailyStatisticsUiTests(Action<bool,string> check) {
            var sampleDay=DateTime.Today.AddDays(-50);var originalClock=store.Clock;var now=sampleDay.AddHours(7).ToUniversalTime();store.Clock=()=>now;
            try {
                for(int i=0;i<4;i++){var t=new TaskItem {Id="daily-ui-task-"+i,Title=new[]{"Học nhập môn lập trình","Code bài tập lập trình","Tự học đại số","Soạn bài buổi tối"}[i],Day=sampleDay.ToString("yyyy-MM-dd"),Time=new[]{"08:00","10:30","14:00","20:00"}[i],Duration=i==0?138:i==1?90:60,Reward=30,Category=i==3?"Công việc":"Học tập"};store.Upsert(t);if(i<3)store.SetDone(t.Id,true);}
                int[] hours={8,10,14},minutes={0,30,0},durations={138,90,60};string[] names={"Học nhập môn lập trình","Code bài tập lập trình","Tự học đại số"};
                for(int i=0;i<3;i++){now=sampleDay.AddHours(hours[i]).AddMinutes(minutes[i]).ToUniversalTime();store.StartFocus(names[i],"Học tập");now=now.AddMinutes(durations[i]);store.FinishFocus();}
                now=sampleDay.AddHours(16).ToUniversalTime();store.Start();now=now.AddMinutes(50);store.Pause();
            }finally{store.Clock=originalClock;}
            statisticsButton.PerformClick();Descendants(statisticsPage).OfType<Button>().First(b=>b.Text=="Một ngày" && b.Visible).PerformClick();var daily=statisticsPage.DailyForTest;daily.SelectDay(sampleDay);Application.DoEvents();
            check(statisticsPage.DailyVisibleForTest && daily.ReportForTest.Day==sampleDay,"one-day statistics opens dedicated daily view from range selector");
            check(daily.ReportForTest.Summary.FocusSeconds==17280 && daily.ReportForTest.Summary.Completed==3 && daily.ReportForTest.Summary.Planned==4 && daily.ReportForTest.Summary.EntertainmentMinutes==50,"daily UI shows selected day focus task and entertainment totals");
            check(daily.RowsForTest==3 && daily.ReportForTest.Summary.EarnedMinutes==90,"daily focus tab lists three sessions with reward summary");
            daily.TabForTest(1);check(daily.RowsForTest==4,"daily work tab lists only scheduled tasks on selected day");
            daily.TabForTest(2);check(daily.RowsForTest==daily.ReportForTest.Wallet.Count && daily.ReportForTest.Wallet.Any(r=>r.Kind=="Giải trí"),"daily wallet tab shows earned credits and entertainment usage");
            daily.TabForTest(0);var size=Size;PerformLayout();Refresh();SaveScreen(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","daily-statistics"),"thong-ke-mot-ngay.png");
            Size=MinimumSize;PerformLayout();Application.DoEvents();check(daily.FitsForTest,"daily statistics fits minimum width without horizontal page scrolling");Refresh();SaveScreen(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","daily-statistics"),"thong-ke-mot-ngay-nho.png");Size=size;PerformLayout();
            daily.MoveForTest(-1);check(daily.ReportForTest.Day==sampleDay.AddDays(-1) && daily.RowsForTest==0 && daily.ReportForTest.Summary.FocusSeconds==0,"previous-day navigation clears stale rows on empty day");
            daily.MoveForTest(1);check(daily.ReportForTest.Day==sampleDay && daily.RowsForTest==3,"next-day navigation restores the selected day's sessions");
            daily.TodayForTest();check(daily.ReportForTest.Day==store.Clock().ToLocalTime().Date,"today shortcut selects current local day");daily.YesterdayForTest();check(daily.ReportForTest.Day==store.Clock().ToLocalTime().Date.AddDays(-1),"yesterday shortcut selects previous local day");
            daily.SelectDay(sampleDay);daily.TabForTest(2);Refresh();SaveScreen(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","daily-statistics"),"vi-theo-ngay.png");daily.TabForTest(0);
            daily.RangeForTest("7 ngày");check(!statisticsPage.DailyVisibleForTest && statisticsPage.BucketsForTest==7,"daily view returns to existing seven-day overview");
            statisticsPage.PresetForTest("Một ngày");check(statisticsPage.DailyVisibleForTest && daily.ReportForTest.Day==sampleDay,"returning to daily view preserves chosen date");statisticsPage.PresetForTest("7 ngày");
        }
        void RunDeadlineSourceUiTests(Action<bool,string> check) {
            string due=DateTime.Today.AddDays(20).ToString("yyyy-MM-dd");
            foreach(string id in new[]{"source-a","source-b","source-empty","source-closed"})store.SaveDeadline(new Deadline {Id=id,Title=id=="source-a"?"Nộp đồ án nhập môn lập trình":id=="source-b"?"Báo cáo cuối tuần":id=="source-empty"?"Deadline chưa có việc":"Deadline đã hoàn tất",DueDay=due});
            store.AddDeadlineTask("source-a",new TaskItem {Id="source-pending",Title="Kiểm thử và sửa lỗi",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="09:00",Duration=90,Reward=30,Category="Học tập",ColorHex="#704DE8"});
            store.AddDeadlineTask("source-b",new TaskItem {Id="source-other",Title="Viết báo cáo",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="09:00",Duration=60,Reward=15,Category="Công việc"});
            store.AddDeadlineTask("source-a",new TaskItem {Id="source-done",Title="Việc đã xong",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="09:00",Duration=60,Reward=0,Category="Học tập"});store.SetDone("source-done",true);
            store.AddDeadlineTask("source-closed",new TaskItem {Id="source-closed-child",Title="Đã bàn giao",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="09:00",Duration=60,Reward=0,Category="Học tập"});store.SetDone("source-closed-child",true);store.CompleteDeadline("source-closed",true);
            store.Upsert(new TaskItem {Id="source-scheduled",Title="Lập trình chức năng chính",Day=DateTime.Today.ToString("yyyy-MM-dd"),Time="10:00",Duration=120,Reward=20,Category="Học tập"});store.AttachTask("source-a","source-scheduled");
            weekButton.PerformClick();int total=store.AllTasks.Count(),scheduledCount=store.Data.Tasks.Count;double credits=store.Data.Balance;DateTime slot=DateTime.Today.AddDays(1).AddHours(15).AddMinutes(15);
            using(var modal=new System.Windows.Forms.Timer {Interval=70}) {
                Action<TaskDialog> action=null;Exception failure=null;
                modal.Tick+=(s,e)=>{var d=Application.OpenForms.OfType<TaskDialog>().FirstOrDefault();if(d==null)return;modal.Stop();try{action(d);}catch(Exception ex){failure=ex;d.DialogResult=DialogResult.Cancel;d.Close();}};
                Action<Action<TaskDialog>> run=body=>{action=body;failure=null;modal.Start();Descendants(this).OfType<Button>().First(b=>b.Text=="+ Thêm việc" && b.Visible).PerformClick();modal.Stop();if(failure!=null)throw failure;Application.DoEvents();};
                run(d=>{
                    check(d.DeadlineSourceForTest!=null && !d.UsesDeadlineSource,"weekly add dialog offers both new-task and deadline-source modes");
                    var title=d.Controls.OfType<TextBox>().Single();title.Text="Việc nháp chưa lưu";d.SelectSourceForTest(true);var p=d.DeadlineSourceForTest;
                    check(!p.TaskChoiceEnabledForTest && !p.ReadyForTest,"deadline must be selected before enabling task selection and save");
                    check(!p.OffersDeadlineForTest("source-closed"),"deadline source hides completed deadlines");
                    p.SelectDeadlineForTest("source-a");check(p.OffersTaskForTest("source-pending") && !p.OffersTaskForTest("source-scheduled") && !p.OffersTaskForTest("source-done") && !p.OffersTaskForTest("source-other"),"default source filter shows only unfinished unscheduled children of selected deadline");
                    p.SelectTaskForTest("source-pending");p.SelectDeadlineForTest("source-b");check(!p.ReadyForTest && !p.OffersTaskForTest("source-pending") && p.OffersTaskForTest("source-other"),"changing parent clears stale task selection and filters dependent list");
                    p.SelectDeadlineForTest("source-empty");check(!p.TaskChoiceEnabledForTest && !p.ReadyForTest,"empty deadline disables task picker and scheduling action");
                    d.SelectSourceForTest(false);check(title.Text=="Việc nháp chưa lưu","switching source modes keeps unsaved new-task draft");d.SelectSourceForTest(true);p.SelectDeadlineForTest("source-a");p.SelectTaskForTest("source-pending");p.SetScheduleForTest(slot,90);
                    d.Refresh();using(var bitmap=new Bitmap(d.Width,d.Height)){d.DrawToBitmap(bitmap,new Rectangle(Point.Empty,d.Size));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","deadline-to-calendar","chon-viec-tu-deadline.png"));}
                    check(d.Controls.Cast<Control>().Where(c=>c.Visible).All(c=>c.Bottom<=d.ClientSize.Height) && d.Height<=Screen.PrimaryScreen.WorkingArea.Height,"deadline source form fits screen with visible footer buttons");
                    d.AcceptButton.PerformClick();
                });
                var placed=store.Data.Tasks.First(t=>t.Id=="source-pending");check(store.AllTasks.Count()==total && store.Data.Tasks.Count==scheduledCount+1 && placed.Day==slot.ToString("yyyy-MM-dd") && placed.Time=="15:15" && placed.Duration==90,"weekly add places original pending task in selected calendar slot exactly once");
                check(placed.Reward==30 && placed.ColorHex=="#704DE8" && !placed.Done && placed.Awarded==0 && store.ParentDeadline(placed.Id).Id=="source-a" && store.Data.Balance==credits,"weekly deadline placement preserves identity details link and credits");
                string beforeCancel=File.ReadAllText(store.FilePath);
                run(d=>{d.SelectSourceForTest(true);var p=d.DeadlineSourceForTest;p.SelectDeadlineForTest("source-b");p.SelectTaskForTest("source-other");d.CancelButton.PerformClick();});
                check(File.ReadAllText(store.FilePath)==beforeCancel && !store.IsScheduled("source-other"),"canceling deadline source leaves saved data and unscheduled task untouched");
                int prompts=0;
                run(d=>{d.SelectSourceForTest(true);var p=d.DeadlineSourceForTest;p.SelectDeadlineForTest("source-a");p.ShowAllForTest();check(p.OffersTaskForTest("source-scheduled") && p.OffersTaskForTest("source-pending") && !p.OffersTaskForTest("source-done"),"all unfinished filter makes already scheduled tasks available without completed tasks");p.SelectTaskForTest("source-scheduled");p.SetScheduleForTest(slot.AddDays(1),45);p.ConfirmForTest(()=>{prompts++;return false;});d.AcceptButton.PerformClick();check(d.Visible && d.Result==null && !p.RescheduleConfirmed,"declining reschedule keeps form open without a committed result");d.CancelButton.PerformClick();});
                check(prompts==1 && store.Data.Tasks.First(t=>t.Id=="source-scheduled").Time=="10:00" && File.ReadAllText(store.FilePath)==beforeCancel,"declining schedule move preserves original calendar block and file");
                run(d=>{d.SelectSourceForTest(true);var p=d.DeadlineSourceForTest;p.SelectDeadlineForTest("source-a");p.ShowAllForTest();p.SelectTaskForTest("source-scheduled");p.SetScheduleForTest(slot.AddDays(1),45);p.ConfirmForTest(()=>{prompts++;return true;});d.AcceptButton.PerformClick();});
                var moved=store.Data.Tasks.First(t=>t.Id=="source-scheduled");check(prompts==2 && moved.Day==slot.AddDays(1).ToString("yyyy-MM-dd") && moved.Time=="15:15" && moved.Duration==45 && store.Data.Tasks.Count(t=>t.Id==moved.Id)==1 && store.AllTasks.Count()==total && store.Data.Balance==credits,"accepting move reschedules one existing task without duplicate or extra reward");
                run(d=>{d.SelectSourceForTest(true);var p=d.DeadlineSourceForTest;p.SelectDeadlineForTest("source-b");p.SelectTaskForTest("source-other");p.SetScheduleForTest(DateTime.Today.AddDays(25).AddHours(9),60);p.ConfirmForTest(()=>{prompts++;return false;});d.AcceptButton.PerformClick();check(d.Visible && d.Result==null,"after-deadline warning can be declined before placing pending work");d.CancelButton.PerformClick();});
                check(prompts==3 && !store.IsScheduled("source-other"),"declining after-deadline warning retains pending task");
                var reloaded=new Store(Path.GetDirectoryName(store.FilePath));check(reloaded.ParentDeadline("source-pending").Id=="source-a" && reloaded.Data.Tasks.Count(t=>t.Id=="source-scheduled")==1,"deadline-to-calendar placement and move survive data reload");
            }
        }
        public void UiTest(string directory) {
            var lines = new System.Collections.Generic.List<string>();
            Action<bool, string> check = (ok, text) => { if (!ok) throw new Exception(text); lines.Add("PASS: " + text); };
            var driver = new System.Windows.Forms.Timer { Interval = 100 };
            try {
                check(store.Data.Tasks.Count == 0, "fresh UI starts empty");
                check(!Descendants(this).OfType<Label>().Any(l => l.Text == "GẦN ĐÂY"), "recent activity panel is removed from the interface");
                check(weekView && calendar != null && title.Text == "Lịch tuần", "weekly planner is the initial screen");
                var initialSize = Size; Size = MinimumSize; PerformLayout(); ResizeBody();
                check(calendar.Width <= body.ClientSize.Width && calendar.Height <= body.ClientSize.Height && !body.HorizontalScroll.Visible, "calendar fits minimum window without horizontal scrolling");
                Size = initialSize; PerformLayout(); ResizeBody();
                calendar.SelectDate(weekStart.AddDays(2));
                check(selectedDay == weekStart.AddDays(2) && calendar.SelectedDay==selectedDay, "calendar day selection works without daily agenda sidebar");
                todayButton.PerformClick();
                check(!Descendants(rightRail).OfType<Label>().Any(l=>l.Text.StartsWith("VIỆC TRONG NGÀY")) && !planningToolbar.Visible && monthlyGoals.ListVisible, "daily agenda is removed while monthly goals remain visible");
                driver.Tick += (s, e) => {
                    var dialog = Application.OpenForms.OfType<TaskDialog>().FirstOrDefault();
                    if (dialog != null) { driver.Stop(); if (dialog.Text == "Sao chép công việc") dialog.SaveForTest(); else dialog.FillForTest("Công việc thử qua giao diện", 12); }
                };
                driver.Start(); Descendants(this).OfType<Button>().First(b => b.Text == "+ Thêm việc").PerformClick(); driver.Stop();
                check(store.Data.Tasks.Count == 1 && store.Data.Tasks[0].Title == "Công việc thử qua giao diện", "add dialog saves Vietnamese title and selected reward");
                var cb = Descendants(body).OfType<CheckBox>().First(); cb.Checked = true; Application.DoEvents();
                check(store.Data.Tasks[0].Done && store.CurrentBalance() == 720, "checkbox event awards exactly twelve minutes");
                play.PerformClick(); check(store.Data.Running && play.Text == "Tạm dừng", "start button enters running state");
                play.PerformClick(); check(!store.Data.Running && play.Text == "Bắt đầu giải trí", "pause button persists paused state");
                driver.Start(); Descendants(body).OfType<Button>().First(b => b.Text == "Sửa").PerformClick(); driver.Stop();
                check(store.Data.Tasks.Count == 1 && store.Data.Tasks[0].Awarded == 12, "edit dialog preserves original earned minutes");
                weekButton.PerformClick(); check(weekView && calendar.VisibleTaskCount == 1, "week navigation shows scheduled task");
                todayButton.PerformClick(); check(!weekView && Descendants(body).OfType<CheckBox>().Count() == 1, "today navigation restores task list");
                cb = Descendants(body).OfType<CheckBox>().First();
                // Repay the small amount consumed during the start/pause check before undo.
                var bonus = new TaskItem { Id = "ui-bonus", Title = "Thưởng kiểm thử", Day = DateTime.Today.ToString("yyyy-MM-dd"), Time = "23:00", Duration = 1, Reward = 1, Category = "Cá nhân" };
                store.Upsert(bonus); store.SetDone(bonus.Id, true);
                cb.Checked = false; Application.DoEvents();
                check(!store.Data.Tasks.First(t => t.Id != "ui-bonus").Done && store.CurrentBalance() < 60 && store.CurrentBalance() > 58, "uncheck event revokes original award");
                var loaded = new Store(Path.GetDirectoryName(store.FilePath));
                check(loaded.Data.Tasks.Count == 2 && !loaded.Data.Running, "UI changes survive a fresh disk read");
                var timelineItems = body.Controls.OfType<TimelineEntry>().ToList();
                check(timelineItems.Count == 2 && timelineItems.Select(t => t.Task.Time).SequenceEqual(timelineItems.Select(t => t.Task.Time).OrderBy(t => t)) && timelineItems.First().First && timelineItems.Last().Last, "today timeline keeps chronological order and endpoints");
                check(todayProgress.Count == 2 && todayProgress.Completed == 1, "today progress follows persisted task completion");
                driver.Tick += (s, e) => {
                    var settings = Application.OpenForms.OfType<SettingsDialog>().FirstOrDefault();
                    if (settings != null) { driver.Stop(); settings.FillForTest(22, 30); }
                    var goals = Application.OpenForms.OfType<GoalsDialog>().FirstOrDefault();
                    if (goals != null) { driver.Stop(); goals.FillForTest(); }
                };
                driver.Start(); settingsButton.PerformClick(); driver.Stop();
                check(store.Data.Settings.DefaultReward == 22 && store.Data.Tasks.First(t => t.Id != "ui-bonus").Reward == 30, "settings UI saves default and per-task reward");
                using (var taskDialog = new TaskDialog(null, DateTime.Today, store.Data.Settings.DefaultReward)) check(taskDialog.RewardForTest == 22, "new task inherits saved default reward");
                driver.Start(); goalsEdit.PerformClick(); driver.Stop();
                check(store.Data.Goals.Count == 1 && store.Data.Goals[0].Current == 8 && monthlyGoals.RowCount == 1, "monthly goals dialog updates the sidebar table");
                weekButton.PerformClick(); string taskId = store.Data.Tasks.First(t => t.Id != "ui-bonus").Id;
                // A midweek date keeps both the source and destination inside the displayed week.
                store.Reschedule(taskId, weekStart.AddDays(2).ToString("yyyy-MM-dd"), "09:00", 60); calendar.SetTasks(store.Data.Tasks);
                double credits = store.CurrentBalance(); calendar.SimulateGesture(taskId, 1, 90, 0, false);
                var movedTask = store.Data.Tasks.First(t => t.Id == taskId);
                check(movedTask.Day == weekStart.AddDays(3).ToString("yyyy-MM-dd") && movedTask.Time == "10:30" && movedTask.Duration == 60, "mouse drag changes day and time while preserving duration");
                calendar.SimulateGesture(taskId, 0, 30, 2, false);
                check(store.Data.Tasks.First(t => t.Id == taskId).Duration == 90, "dragging bottom edge changes duration");
                calendar.SimulateGesture(taskId, 0, 15, 1, false);
                check(store.Data.Tasks.First(t => t.Id == taskId).Time == "10:45" && store.Data.Tasks.First(t => t.Id == taskId).Duration == 75, "dragging top edge changes start while preserving end");
                calendar.SimulateGesture(taskId, 1, 30, 0, true);
                check(store.Data.Tasks.First(t => t.Id == taskId).Time == "10:45" && store.CurrentBalance() == credits, "cancelling a drag preserves schedule and rewards");
                store.Reschedule("ui-bonus", store.Data.Tasks.First(t => t.Id == taskId).Day, "11:00", 60); calendar.SetTasks(store.Data.Tasks);
                check(calendar.OverlapHasSeparateLanes(), "overlapping blocks receive separate visual lanes");
                double beforeCalendarCheck = store.CurrentBalance(); int calendarReward = store.Data.Tasks.First(t => t.Id == taskId).Reward;
                calendar.ToggleForTest(taskId); Application.DoEvents();
                check(store.Data.Tasks.First(t => t.Id == taskId).Done && Math.Abs(store.CurrentBalance() - beforeCalendarCheck - calendarReward * 60) < .01, "pastel card checkbox awards the scheduled reward once");
                calendar.ToggleForTest(taskId); Application.DoEvents();
                check(!store.Data.Tasks.First(t => t.Id == taskId).Done && Math.Abs(store.CurrentBalance() - beforeCalendarCheck) < .01, "pastel card checkbox can undo completion without moving the task");
                var persisted = new Store(Path.GetDirectoryName(store.FilePath));
                check(persisted.Data.Settings.DefaultReward == 22 && persisted.Data.Goals.Count == 1 && persisted.Data.Tasks.First(t => t.Id == taskId).Time == "10:45", "goals settings and dragged schedule survive reopen");
                double oldBalance = store.CurrentBalance();
                monthlyGoals.ToggleForTest(0, true); Application.DoEvents();
                check(Store.GoalIsDone(store.Data.Goals[0]) && store.CurrentBalance() == oldBalance, "monthly goal checkbox completes without changing wallet");
                check(monthlyGoals.CompletedCount == 1 && store.Data.Goals[0].Current == 8 && store.Data.Goals[0].Target == 20, "goal progress updates without overwriting numeric targets");
                check(monthlyGoals.PendingRowsForTest==0 && monthlyGoals.DoneRowsForTest==1,"ticking goal moves it into completed group");
                monthlyGoals.ToggleForTest(0, false); Application.DoEvents();
                check(!Store.GoalIsDone(store.Data.Goals[0]), "monthly goal can be reopened by unchecking");
                check(monthlyGoals.PendingRowsForTest==1 && monthlyGoals.DoneRowsForTest==0,"unticking goal returns it to incomplete group");
                monthlyGoals.MoveMonthForTest(1);
                check(monthlyGoals.RowCount == 0 && monthlyGoals.CompletedCount == 0 && store.Data.Goals.Count == 1, "empty month shows no stale goals and preserves stored records");
                monthlyGoals.MoveMonthForTest(-1);
                check(monthlyGoals.RowCount == 1 && monthlyGoals.CompletedCount == 0 && store.Data.Goals[0].Current == 8, "returning to a month restores its saved goals and quantities");
                check(title.Text == "Lịch tuần" && motivation.Text == WeeklyMotivation.ForWeek(weekStart), "weekly header retains the quote for the displayed week");
                driver.Tick += (s, e) => { var colors = Application.OpenForms.OfType<TaskColorDialog>().FirstOrDefault(); if (colors != null) { driver.Stop(); colors.ChooseForTest(); } };
                driver.Start(); calendar.InvokeMenuForTest(taskId, "Đổi màu…"); driver.Stop();
                check(store.Data.Tasks.First(t => t.Id == taskId).ColorHex == "#C14687", "calendar color menu saves selected swatch");
                int beforeCopy = store.Data.Tasks.Count;
                driver.Start(); calendar.InvokeMenuForTest(taskId, "Sao chép…"); driver.Stop();
                var copied = store.Data.Tasks.Last();
                check(store.Data.Tasks.Count == beforeCopy + 1 && copied.Id != taskId && copied.ColorHex == "#C14687" && copied.Time == "10:45" && copied.Duration == 75 && copied.Reward == 30 && !copied.Done && copied.Awarded == 0 && store.CurrentBalance() == oldBalance, "calendar copy menu saves independent draft with color schedule and reward");
                persisted = new Store(Path.GetDirectoryName(store.FilePath));
                check(!Store.GoalIsDone(persisted.Data.Goals[0]) && persisted.Data.Tasks.Last().ColorHex == "#C14687", "goal checkbox and custom task colors persist after reopen");
                focusButton.PerformClick(); check(activePage == 1 && focusPage.Visible, "focus navigation opens dedicated stopwatch page");
                check(focusPage.IdleUiForTest, "idle focus page offers start and disables saving an empty session");
                var focusFullSize = Size; Size = MinimumSize; PerformLayout(); focusPage.PerformLayout();
                check(focusPage.FitsForTest, "focus controls and history fit the minimum window" + (focusPage.FitsForTest ? "" : " " + focusPage.LayoutForTest));
                Size = focusFullSize; PerformLayout();
                var realClock = store.Clock; var focusNow = DateTime.UtcNow; store.Clock = () => focusNow;
                focusPage.ChooseWorkForTest(); focusPage.StartForTest(); focusNow = focusNow.AddSeconds(65); focusPage.RefreshState();
                check(store.Data.ActiveFocus.Category == "Công việc", "activity buttons save the chosen focus category");
                check(focusPage.RunningUiForTest && focusPage.TodayTimeForTest == "00:01:05", "running focus page shows pause and includes active time in today's total");
                Refresh();
                using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "bam-gio-dang-chay.png")); }
                focusPage.PauseForTest();
                check(store.Data.ActiveFocus != null && !store.Data.ActiveFocus.Running && store.FocusSeconds() == 65, "focus UI starts and pauses stopwatch");
                check(focusPage.PausedUiForTest, "paused focus page offers resume and save");
                Refresh();
                using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "bam-gio-tam-dung.png")); }
                focusNow = focusNow.AddSeconds(30); focusPage.ResumeForTest(); focusNow = focusNow.AddSeconds(55); focusPage.FinishForTest();
                check(store.Data.ActiveFocus == null && store.Data.FocusSessions.Count == 1 && Store.SliceSeconds(store.Data.FocusSessions[0].Slices) == 120, "focus UI saves actual time excluding break");
                check(focusPage.IdleUiForTest && focusPage.TodayTimeForTest == "00:02:00", "saving resets the dial and counts the saved session once in today's summary");
                store.Clock = realClock;
                statisticsButton.PerformClick(); check(activePage == 2 && statisticsPage.Visible && statisticsPage.RowsForTest >= 1, "statistics page shows filtered long term table");
                check(statisticsPage.BucketsForTest == 7 && statisticsPage.FocusSecondsForTest == 120 && statisticsPage.ChartSecondsForTest == 120 && statisticsPage.DistributionSecondsForTest == 120, "seven-day overview chart and distribution agree with the saved session");
                check(!monthlyGoals.ListVisible, "statistics sidebar retains collapsed monthly goals");
                goalsToggle.PerformClick();
                check(monthlyGoals.ListVisible && !Descendants(rightRail).OfType<Label>().Any(l=>l.Text.StartsWith("VIỆC TRONG NGÀY")), "statistics expands goals without restoring removed daily agenda");
                goalsToggle.PerformClick();
                var statsFullSize = Size; Size = MinimumSize; PerformLayout(); statisticsPage.PerformLayout();
                check(statisticsPage.FitsForTest, "statistics cards and charts fit minimum width using vertical scrolling");
                Size = statsFullSize; PerformLayout();
                statisticsPage.GroupForTest("Tuần");
                check(statisticsPage.FocusSecondsForTest == 120 && statisticsPage.GoalsForTest == 1, "weekly grouping preserves time and counts monthly goals once");
                statisticsPage.PresetForTest("12 tháng"); statisticsPage.ShowEmptyForTest(true);
                check(statisticsPage.BucketsForTest == 12 && statisticsPage.RowsForTest == 12 && statisticsPage.FocusSecondsForTest == 120, "twelve-month preset retains zero periods and correct totals");
                statisticsPage.ShowEmptyForTest(false);
                check(statisticsPage.RowsForTest < 12 && statisticsPage.DistributionSecondsForTest == 120, "hiding empty periods changes table rows without changing totals");
                statisticsPage.ShowDetailsForTest(true);
                check(statisticsPage.DetailColumnsForTest == 9, "extra detail columns retain rewards refunds leisure and goals");
                statisticsPage.ShowDetailsForTest(false);
                check(statisticsPage.DetailColumnsForTest == 5, "compact detail table restores five readable columns");
                statisticsPage.PresetForTest("7 ngày"); statisticsPage.RangeForTest(DateTime.Today.AddYears(-2).AddDays(-6), DateTime.Today.AddYears(-2));
                check(statisticsPage.RowsForTest == 0 && statisticsPage.ChartSecondsForTest == 0 && statisticsPage.DistributionSecondsForTest == 0 && statisticsPage.GoalsForTest == 0, "empty custom range clears rows chart distribution and goals together");
                Refresh();
                using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(directory, "thong-ke-trong.png")); }
                statisticsPage.PresetForTest("90 ngày"); check(statisticsPage.FocusSecondsForTest == 120, "ninety-day preset preserves saved focus time");
                statisticsPage.PresetForTest("Tất cả"); check(statisticsPage.FocusSecondsForTest == 120, "all-time preset includes the saved focus session");
                statisticsPage.PresetForTest("7 ngày"); focusButton.PerformClick();
                check(monthlyGoals.ListVisible, "leaving statistics restores expanded monthly goals");
                statisticsButton.PerformClick();
                DateTime pastDay = DateTime.Today.AddDays(-140);
                store.Upsert(new TaskItem { Id = "past-week", Title = "Lịch cũ", Day = pastDay.ToString("yyyy-MM-dd"), Time = "09:00", Duration = 30, Reward = 0, Category = "Học tập" });
                driver.Tick += (s, e) => { var lookupDialog = Application.OpenForms.OfType<WeekLookupDialog>().FirstOrDefault(); if (lookupDialog != null) { driver.Stop(); lookupDialog.ChooseForTest(pastDay); } };
                driver.Start(); Descendants(this).OfType<Button>().First(b => b.Text == "Tra cứu tuần").PerformClick(); driver.Stop();
                check(activePage == 0 && weekView && weekStart == Store.WeekOf(pastDay) && calendar.VisibleTaskCount == 1, "week lookup opens preserved tasks from older week");
                check(motivation.Text == WeeklyMotivation.ForWeek(pastDay), "past week uses matching weekly quote");
                calendar.SelectDate(pastDay);
                check(selectedDay==pastDay && calendar.VisibleTaskCount==1 && store.Data.Tasks.Any(t=>t.Id=="past-week" && t.Title=="Lịch cũ"), "selected past date retains preserved task on calendar without agenda");
                check(complete.Text == "0 / 1 hoàn thành" && earned.Text == "+0 phút", "weekly summaries use the displayed week");
                check(new Store(Path.GetDirectoryName(store.FilePath)).Data.FocusSessions.Count == 1, "focus UI saved session persists on disk");
                todayButton.PerformClick();
                int todayCount = store.Data.Tasks.Count(t => t.Day == Key(DateTime.Today)); int allCount = store.Data.Tasks.Count; double beforeExpansion = store.CurrentBalance();
                check(!overdueExpanded && Descendants(body).OfType<CheckBox>().Count() == todayCount, "overdue tasks start collapsed outside today's timeline");
                Descendants(body).OfType<Button>().First(b => b.Name == "OverdueToggle").PerformClick();
                int overdueCount = store.Data.Tasks.Count(t => String.CompareOrdinal(t.Day, Key(DateTime.Today)) < 0 && !t.Done);
                check(Descendants(body).OfType<CheckBox>().Count() == todayCount + overdueCount && todayProgress.Count == todayCount && store.Data.Tasks.Count == allCount && store.CurrentBalance() == beforeExpansion, "expanding overdue tasks preserves today's totals and wallet");
                var oldTaskCard = body.Controls.OfType<Card>().First(c => Convert.ToString(c.Tag) == "overdue:past-week");
                Descendants(oldTaskCard).OfType<CheckBox>().First().Checked = true; Application.DoEvents();
                check(store.Data.Tasks.First(t => t.Id == "past-week").Done && todayProgress.Count == todayCount && store.CurrentBalance() == beforeExpansion, "completing a zero-reward overdue task does not inflate today's progress");
                overdueExpanded = false; Render();
                var timelineFullSize = Size; Size = MinimumSize; PerformLayout(); ResizeBody();
                check(!body.HorizontalScroll.Visible && body.Controls.OfType<TimelineEntry>().All(t => t.Right <= body.ClientSize.Width), "today timeline fits minimum width without horizontal scrolling");
                Size = timelineFullSize; PerformLayout();
                var sampleGoals = Enumerable.Range(0, 12).Select(i => new MonthlyGoal { Id = "layout-" + i, Month = goalsMonth.ToString("yyyy-MM"), Title = i == 0 ? "Hoàn thành dự án lập trình cá nhân" : "Mục tiêu kiểm tra " + (i + 1), Done = i == 1 || i == 2, Target = 1 }).ToList();
                monthlyGoals.Bind(goalsMonth, sampleGoals.Take(6));
                check(monthlyGoals.RowCount == 6 && monthlyGoals.RowsFitForTest && monthlyGoals.TitlesFitForTest && monthlyGoals.AllRowsVisibleForTest, "six goals fit the expanded list with complete two-line titles");
                check(monthlyGoals.PendingRowsForTest==4 && monthlyGoals.DoneRowsForTest==2,"goals are separated by completion with stable counts");
                monthlyGoals.ToggleGroupForTest(true);check(monthlyGoals.DoneRowsForTest==0 && monthlyGoals.PendingRowsForTest==4 && monthlyGoals.CompletedCount==2 && monthlyGoals.RowCount==6,"collapsing completed group preserves data and progress");
                monthlyGoals.ToggleGroupForTest(false);check(monthlyGoals.PendingRowsForTest==0 && monthlyGoals.DoneRowsForTest==0 && monthlyGoals.EditButton.Visible,"both goal groups can collapse while edit action stays available");
                monthlyGoals.ToggleGroupForTest(true);monthlyGoals.ToggleGroupForTest(false);check(monthlyGoals.PendingRowsForTest==4 && monthlyGoals.DoneRowsForTest==2 && monthlyGoals.TitlesFitForTest,"expanding groups restores all goal titles");
                monthlyGoals.Bind(goalsMonth, sampleGoals);
                check(monthlyGoals.RowCount == 12 && monthlyGoals.RowsFitForTest && monthlyGoals.EditButton.Bottom <= monthlyGoals.Height && monthlyGoals.EditButton.Visible, "long goal lists scroll vertically while keeping the edit button accessible");
                string[] ringTitles={"Đọc sách mỗi ngày","Hoàn thành tài liệu dự án","Hoàn thành dự án lập trình cá nhân","Hoàn thành bộ bài tập lập trình","Luyện lập trình C"};
                monthlyGoals.Bind(goalsMonth,ringTitles.Select((text,i)=>new MonthlyGoal {Id="ring-demo-"+i,Month=goalsMonth.ToString("yyyy-MM"),Title=text,Target=1,Done=i==1||i==2}));PerformLayout();monthlyGoals.Refresh();
                check(monthlyGoals.RowCount==5 && monthlyGoals.CompletedCount==2 && monthlyGoals.TitlesFitForTest && monthlyGoals.AllRowsVisibleForTest,"five reference goals fit ring layout with complete long titles");
                using(var bitmap=new Bitmap(monthlyGoals.Width,monthlyGoals.Height)){monthlyGoals.DrawToBitmap(bitmap,new Rectangle(Point.Empty,monthlyGoals.Size));bitmap.Save(Path.Combine(directory,"goals-ring","muc-tieu-mau3.png"));}
                UpdateGoals();
                RunDeadlineUiTests(check);
                RunDeadlineMenuUiTests(check);
                RunTaskDeadlineUiTests(check);
                RunFocusTaskUiTests(check);
                RunDailyStatisticsUiTests(check);
                RunDeadlineSourceUiTests(check);
                WritePreview(directory);
                lines.Add("ALL " + lines.Count + " UI CHECKS PASSED");
                File.WriteAllLines(Path.Combine(directory, "ui-tests.txt"), lines);
            } catch (Exception ex) { lines.Add("FAIL: " + ex); File.WriteAllLines(Path.Combine(directory, "ui-tests.txt"), lines); }
            finally { driver.Stop(); driver.Dispose(); Close(); }
        }
    }
    static class Program {
        static void SeedDeadlines(Store store) {
            var d = new Deadline { Id="demo-project", Title="Nộp đồ án nhập môn lập trình", DueDay=DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"), DueTime="23:59", Notes="Kiểm tra bản cuối, chạy thử và nộp đầy đủ mã nguồn cùng báo cáo." }; store.SaveDeadline(d);
            string[] titles={"Đọc yêu cầu và phân tích đề bài","Thiết kế chương trình","Lập trình chức năng chính","Kiểm thử và sửa lỗi","Nộp bài và bàn giao"};
            for(int i=0;i<5;i++) { var t=new TaskItem {Id="demo-child-"+i,Title=titles[i],Day=DateTime.Today.AddDays(i-2).ToString("yyyy-MM-dd"),Time="14:00",Duration=i==2?120:60,Reward=20,Category="Học tập"}; store.AddDeadlineTask(d.Id,t);if(i<3)store.ScheduleDeadlineTask(t);if(i<2)store.SetDone(t.Id,true); }
            store.SaveDeadline(new Deadline {Id="demo-soon",Title="Hoàn thành báo cáo tuần",DueDay=DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"),DueTime="18:00"});
            store.SaveDeadline(new Deadline {Id="demo-late",Title="Ôn tập giải tích giữa kỳ",DueDay=DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd")});
        }
        [STAThread] static void Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            bool deadlinePreview = args.Length > 0 && args[0] == "--deadline-preview";
            bool todayPreview = args.Length > 0 && args[0] == "--today-preview";
            bool preview = deadlinePreview || todayPreview || args.Length > 0 && args[0] == "--preview";
            bool uiTest = args.Length > 0 && args[0] == "--ui-test";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            bool owner;
            string id = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(baseDir.ToLowerInvariant() + (preview || uiTest ? "_verify" : "")))).Replace('/', '_').Replace('+', '_');
            using (var mutex = new Mutex(true, "Local\\Nhip_" + id, out owner)) {
                if (!owner) { MessageBox.Show("Nhịp đang chạy. Hãy mở cửa sổ Nhịp trên thanh tác vụ.", "Nhịp"); return; }
                try {
                    string directory = uiTest ? Path.Combine(baseDir, "verification", "ui-data-" + DateTime.UtcNow.Ticks) : deadlinePreview ? Path.Combine(baseDir, "verification", "deadline-preview-data") : todayPreview ? Path.Combine(baseDir, "verification", "today-preview-data") : preview ? Path.Combine(baseDir, "verification", "preview-data") : Path.Combine(baseDir, "data");
                    var store = new Store(directory);
                    if (todayPreview && store.Data.Tasks.Count == 0) {
                        string[] sampleNames = { "Học trực tiếp: Nhập môn lập trình", "Code bài tập lập trình", "Tự học đại số buổi 13", "Soạn đề thi thử toán đợt 6", "Tự học: giải đề thi thử giữa kì giải tích 1 + chữa" };
                        string[] sampleTimes = { "06:45", "13:30", "15:00", "20:00", "20:00" }; int[] sampleDurations = { 150, 60, 120, 120, 120 };
                        for (int i = 0; i < 5; i++) store.Upsert(new TaskItem { Id = "today-sample-" + i, Title = sampleNames[i], Day = DateTime.Today.AddDays(i == 4 ? -1 : 0).ToString("yyyy-MM-dd"), Time = sampleTimes[i], Duration = sampleDurations[i], Reward = i == 0 ? 20 : 30, Category = i == 3 ? "Công việc" : "Học tập", ColorHex = i == 3 ? "#C84143" : "#25834D" });
                        store.SetDone("today-sample-0", true);
                        string sampleMonth = DateTime.Today.ToString("yyyy-MM");
                        string[] sampleGoals = { "Đọc sách mỗi ngày", "Hoàn thành tài liệu dự án", "Hoàn thành dự án lập trình", "Ôn tập học phần", "Đọc sách", "Vận động đều" };
                        store.SaveGoals(sampleMonth, sampleGoals.Select((text, i) => new MonthlyGoal { Id = "today-goal-" + i, Month = sampleMonth, Title = text, Current = i == 1 || i == 2 ? 1 : 0, Target = 1, Done = i == 1 || i == 2, Unit = "mục tiêu" }).ToList());
                    }
                    if (preview && store.Data.Tasks.Count == 0) {
                        string[] names = { "Ôn tập Toán · 20 câu tư duy", "Đi bộ và vận động nhẹ", "Đọc 20 trang sách", "Hoàn thành dự án cá nhân", "Lên kế hoạch cho tuần mới" };
                        string[] cats = { "Học tập", "Vận động", "Cá nhân", "Công việc", "Cá nhân" };
                        for (int i = 0; i < names.Length; i++) {
                            var t = new TaskItem { Id = "preview" + i, Title = names[i], Day = DateTime.Today.AddDays(i < 3 ? 0 : i - 2).ToString("yyyy-MM-dd"), Time = (8 + i * 2).ToString("00") + ":00", Duration = i == 1 ? 30 : 60, Reward = i == 0 ? 20 : 15, Category = cats[i] };
                            store.Upsert(t); if (i < 2) store.SetDone(t.Id, true);
                        }
                    }
                    if (preview) {
                        if (store.Data.FocusSessions.Count == 0) {
                            var normalClock = store.Clock;
                            for (int i = 0; i < 18; i++) {
                                DateTime sample = DateTime.Today.AddDays(-i * 5).AddHours(9).ToUniversalTime(); store.Clock = () => sample;
                                store.StartFocus(i % 2 == 0 ? "Ôn tập tư duy Toán" : "Dự án cá nhân", i % 2 == 0 ? "Học tập" : "Công việc");
                                sample = sample.AddMinutes(30 + (i % 4) * 20); store.FinishFocus();
                            }
                            store.Clock = normalClock;
                        }
                        string month = DateTime.Today.ToString("yyyy-MM");
                        if (!store.Data.Goals.Any(g => g.Month == month)) store.SaveGoals(month, new System.Collections.Generic.List<MonthlyGoal> {
                            new MonthlyGoal { Id = "preview-goal1", Month = month, Title = "Đọc sách", Current = 8, Target = 20, Unit = "trang" },
                            new MonthlyGoal { Id = "preview-goal2", Month = month, Title = "Vận động", Current = 12, Target = 20, Unit = "buổi" },
                            new MonthlyGoal { Id = "preview-goal3", Month = month, Title = "Hoàn thành dự án", Current = 1, Target = 1, Unit = "dự án" }
                        });
                        var p3 = store.Data.Tasks.FirstOrDefault(t => t.Id == "preview3");
                        if (p3 != null) store.Reschedule(p3.Id, p3.Day, "09:00", 90);
                        var p4 = store.Data.Tasks.FirstOrDefault(t => t.Id == "preview4");
                        if (p4 != null) store.Reschedule(p4.Id, p4.Day, "11:00", 60);
                    }
                    if (deadlinePreview && store.Data.Deadlines.Count == 0) SeedDeadlines(store);
                    bool interrupted = store.RecoverInterruptedFocus();
                    var form = new MainForm(store);
                    if (interrupted) form.Shown += (s, e) => MessageBox.Show(form, "Phiên bấm giờ trước bị gián đoạn. App đã khôi phục và tạm dừng ở mốc lưu gần nhất, không cộng khoảng thời gian app đóng. Vào Bấm giờ để tiếp tục hoặc lưu phiên.", "Đã khôi phục phiên bấm giờ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    if (uiTest) form.Shown += (s, e) => form.BeginInvoke((Action)(() => form.UiTest(Path.Combine(baseDir, "verification"))));
                    if (preview) form.Shown += (s, e) => form.BeginInvoke((Action)(() => {
                        try { if (deadlinePreview) form.WriteDeadlinePreview(Path.Combine(baseDir, "verification", "deadlines")); else form.WritePreview(Path.Combine(baseDir, "verification")); }
                        catch (Exception ex) { File.WriteAllText(Path.Combine(baseDir, "verification", "preview-error.txt"), ex.ToString()); }
                        form.Close();
                    }));
                    Application.Run(form);
                } catch (Exception ex) {
                    MessageBox.Show("Không thể mở Nhịp. Dữ liệu hiện có được giữ nguyên.\n\n" + ex.Message + "\n\nNếu app nằm trong thư mục chỉ đọc, hãy chuyển cả thư mục Nhịp sang Documents.", "Nhịp", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
