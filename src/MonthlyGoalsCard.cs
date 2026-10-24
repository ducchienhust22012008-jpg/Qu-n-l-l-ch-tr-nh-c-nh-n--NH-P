using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Nhip {
    sealed class MonthlyGoalRow : Panel {
        public readonly MonthlyGoal Goal;
        public readonly CheckBox Check;
        readonly Label title;
        public MonthlyGoalRow(MonthlyGoal goal, Action<string, bool> toggle, Action edit, ToolTip tips) {
            Goal = goal; Height = 46; Margin = new Padding(0); DoubleBuffered = true;
            bool done = Store.GoalIsDone(goal); BackColor = done ? Theme.Tint(Theme.Green,.055) : Color.White;
            Check = new CheckBox { Checked = done, Cursor = Cursors.Hand, AutoSize = false, BackColor = BackColor, AccessibleName = goal.Title };
            Controls.Add(Check);
            title = Theme.Label(goal.Title, 9.5f, done ? Theme.Green : Theme.Ink); title.TextAlign = ContentAlignment.MiddleLeft; Controls.Add(title);
            string details = goal.Title + (goal.Target > 0 ? "\n" + goal.Current + " / " + goal.Target + " " + goal.Unit : "");
            tips.SetToolTip(title, details); tips.SetToolTip(Check, details);
            Check.CheckedChanged += (s, e) => toggle(goal.Id, Check.Checked);
            title.DoubleClick += (s, e) => edit(); DoubleClick += (s, e) => edit();
            Resize += (s, e) => FitTitle();
        }
        void FitTitle() {
            int width=Math.Max(20,Width-49);
            int measured=TextRenderer.MeasureText(Goal.Title,title.Font,new Size(Math.Max(10,width-4),Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding).Height;
            int desired=Math.Max(46,measured+16);if(Height!=desired)Height=desired;
            title.SetBounds(40,7,width,Height-14);Check.SetBounds(11,(Height-22)/2,22,22);
        }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using (var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, 10, Height - 1, Width-10, Height - 1); }
        public bool TitleFitsForTest { get { return TextRenderer.MeasureText(Goal.Title, title.Font, new Size(Math.Max(10,title.Width-4), Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height <= title.Height; } }
    }
    sealed class GoalProgressRing : Control {
        public int Completed,Count;
        public GoalProgressRing(){DoubleBuffered=true;BackColor=Theme.Sidebar;AccessibleName="Tỷ lệ hoàn thành mục tiêu tháng";}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            int size=Math.Max(20,Math.Min(Width,Height)-14);var r=new Rectangle((Width-size)/2,(Height-size)/2,size,size);
            double fraction=Count>0?Math.Max(0,Math.Min(1,(double)Completed/Count)):0;
            using(var track=new Pen(Color.FromArgb(226,220,247),7))g.DrawEllipse(track,r);
            if(fraction>0)using(var pen=new Pen(Theme.Purple,7)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawArc(pen,r,-90,(float)(fraction*360));}
            using(var font=Theme.Font(14,true))TextRenderer.DrawText(g,(fraction*100).ToString("0")+"%",font,ClientRectangle,Theme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            AccessibleDescription=Completed+" / "+Count+" mục tiêu hoàn thành";
        }
    }
    sealed class MonthlyGoalsCard : Card {
        public readonly Button EditButton, CollapseButton;
        readonly Button previous, next;
        readonly Label heading, monthLabel, summary, remaining,summaryCaption;
        readonly Card progressCard;
        readonly GoalProgressRing progress;
        readonly FlowLayoutPanel list;
        readonly ToolTip tips = new ToolTip { InitialDelay = 400, AutoPopDelay = 15000 };
        List<MonthlyGoal> goals=new List<MonthlyGoal>();
        DateTime month; bool expanded = true, pendingOpen=true, completedOpen=true,arranging; int count,expandedHeight=420;
        public event Action<DateTime> MonthChanged;
        public event Action<string, bool> CompletionRequested;
        public event Action EditRequested, CollapseRequested;
        public MonthlyGoalsCard() {
            Width = 278; Height = 476; Margin = new Padding(0, 0, 0, 14);
            heading = Theme.Label("MỤC TIÊU THÁNG", 10, Theme.Purple, true); Controls.Add(heading);
            CollapseButton = Theme.Button("⌄"); CollapseButton.FlatAppearance.BorderSize = 0; Controls.Add(CollapseButton); CollapseButton.Click += (s, e) => { if (CollapseRequested != null) CollapseRequested(); };
            previous = Theme.Button("‹"); next = Theme.Button("›"); monthLabel = Theme.Label("", 10, Theme.Ink, true); monthLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(previous); Controls.Add(monthLabel); Controls.Add(next);
            previous.Click += (s, e) => { if (MonthChanged != null) MonthChanged(month.AddMonths(-1)); }; next.Click += (s, e) => { if (MonthChanged != null) MonthChanged(month.AddMonths(1)); };
            progressCard = new Card { BackColor = Theme.Sidebar,Padding=new Padding(0) }; Controls.Add(progressCard);
            progress = new GoalProgressRing { BackColor = progressCard.BackColor }; progressCard.Controls.Add(progress);
            summary = Theme.Label("", 20, Theme.Ink, true); progressCard.Controls.Add(summary);
            summaryCaption=Theme.Label("mục tiêu hoàn thành",8.5f,Theme.Muted);progressCard.Controls.Add(summaryCaption);
            remaining = Theme.Label("", 8.5f, Theme.Muted); progressCard.Controls.Add(remaining);
            list = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.White, Margin = new Padding(0) }; Controls.Add(list);
            EditButton = Theme.Button("+ Thêm / Cập nhật"); EditButton.ForeColor = Theme.Purple; EditButton.FlatAppearance.BorderColor = Theme.Purple; EditButton.Font = Theme.Font(9, true); Controls.Add(EditButton);
            EditButton.Click += (s, e) => { if (EditRequested != null) EditRequested(); };
            Resize += (s, e) => Arrange(); list.ClientSizeChanged += (s, e) => Arrange(); Arrange();
        }
        public int ExpandedHeight { get { return expandedHeight; } }
        public bool ListVisible { get { return list.Visible; } }
        public int RowCount { get { return count; } }
        public int CompletedCount { get { return progress.Completed; } }
        public void Bind(DateTime selectedMonth, IEnumerable<MonthlyGoal> values) {
            if(month.Year!=selectedMonth.Year || month.Month!=selectedMonth.Month)pendingOpen=completedOpen=true;
            month = selectedMonth; goals=values.ToList();count=goals.Count;
            monthLabel.Text = "Tháng " + month.ToString("MM/yyyy");
            int done = goals.Count(Store.GoalIsDone); summary.Text = done + "/" + count;
            progress.Completed = done; progress.Count = count; progress.Invalidate(); remaining.Text = count > 0 ? "Còn " + (count - done) + " mục tiêu" : "Thêm mục tiêu đầu tiên";
            BuildRows();
        }
        void BuildRows(){
            list.SuspendLayout();foreach(Control child in list.Controls.Cast<Control>().ToArray())child.Dispose();
            if(count==0){var empty=Theme.Label("Chưa có mục tiêu.\nBấm + Thêm / Cập nhật để bắt đầu.",9,Theme.Muted);empty.Height=110;empty.Margin=new Padding(0);empty.TextAlign=ContentAlignment.MiddleCenter;list.Controls.Add(empty);}
            else {
                AddGroup(false,goals.Where(g=>!Store.GoalIsDone(g)).ToList(),pendingOpen);
                AddGroup(true,goals.Where(Store.GoalIsDone).ToList(),completedOpen);
            }
            list.ResumeLayout();Arrange();
        }
        void AddGroup(bool done,List<MonthlyGoal> items,bool open){
            var header=Theme.Button((done?"ĐÃ HOÀN THÀNH":"CHƯA HOÀN THÀNH")+" · "+items.Count+"  "+(open?"⌃":"⌄"));header.Font=Theme.Font(8,true);header.TextAlign=ContentAlignment.MiddleLeft;header.Height=32;header.Margin=new Padding(0,done?10:0,0,0);header.Padding=new Padding(2,0,0,0);header.BackColor=done?Theme.Tint(Theme.Green,.09):Theme.Sidebar;header.ForeColor=done?Theme.Green:Theme.Ink;header.FlatAppearance.BorderSize=0;header.AccessibleName=done?"Nhóm mục tiêu đã hoàn thành":"Nhóm mục tiêu chưa hoàn thành";
            header.Click+=(s,e)=>{if(done)completedOpen=!completedOpen;else pendingOpen=!pendingOpen;BuildRows();};list.Controls.Add(header);
            if(open)foreach(var goal in items)list.Controls.Add(new MonthlyGoalRow(goal,(id,complete)=>{if(CompletionRequested!=null)CompletionRequested(id,complete);},()=>{if(EditRequested!=null)EditRequested();},tips));
        }
        public void SetExpanded(bool value, bool collapsible) {
            expanded = value; CollapseButton.Visible = collapsible; CollapseButton.Text = value ? "⌃" : "⌄";
            foreach (Control control in new Control[] { previous, monthLabel, next, progressCard, list, EditButton }) control.Visible = value;
            Height = value ? ExpandedHeight : 50; Arrange();
        }
        void Arrange() {
            if(arranging || list==null)return;arranging=true;
            try {
                int w=ClientSize.Width;
                heading.SetBounds(18,10,w-58,30);CollapseButton.SetBounds(w-41,11,27,27);
                previous.SetBounds(16,46,30,30);next.SetBounds(w-46,46,30,30);monthLabel.SetBounds(49,46,Math.Max(20,w-98),30);
                progressCard.SetBounds(12,87,w-24,108);progress.SetBounds(6,11,84,84);summary.SetBounds(100,9,w-138,36);summaryCaption.SetBounds(100,46,w-138,25);remaining.SetBounds(100,73,w-138,25);
                list.AutoScroll=false;list.AutoScrollMinSize=Size.Empty;
                int width=Math.Max(80,w-26);
                foreach(Control row in list.Controls)row.Width=width;
                int total=list.Controls.Cast<Control>().Sum(c=>c.Height+c.Margin.Vertical);
                expandedHeight=Math.Max(420,Math.Min(680,263+total));if(expanded)Height=expandedHeight;
                list.SetBounds(12,203,w-24,Math.Max(20,Height-263));
                if(total>list.ClientSize.Height){width-=SystemInformation.VerticalScrollBarWidth;foreach(Control row in list.Controls)row.Width=Math.Max(80,width);}
                list.PerformLayout();list.AutoScroll=true;list.PerformLayout();EditButton.SetBounds(16,Height-46,w-32,34);
            } finally {arranging=false;}
        }
        public void ToggleForTest(int index, bool done) { string id=goals[index].Id;list.Controls.OfType<MonthlyGoalRow>().First(r=>r.Goal.Id==id).Check.Checked = done; }
        public void MoveMonthForTest(int direction) { if (direction < 0) previous.PerformClick(); else next.PerformClick(); }
        public bool RowsFitForTest { get { return list.Controls.OfType<MonthlyGoalRow>().All(r => r.Right <= list.ClientSize.Width) && !list.HorizontalScroll.Visible; } }
        public bool TitlesFitForTest { get { return list.Controls.OfType<MonthlyGoalRow>().All(r => r.TitleFitsForTest); } }
        public bool AllRowsVisibleForTest { get { return list.Controls.OfType<MonthlyGoalRow>().All(r => r.Top >= 0 && r.Bottom <= list.ClientSize.Height); } }
        public int PendingRowsForTest { get { return list.Controls.OfType<MonthlyGoalRow>().Count(r=>!Store.GoalIsDone(r.Goal)); } }
        public int DoneRowsForTest { get { return list.Controls.OfType<MonthlyGoalRow>().Count(r=>Store.GoalIsDone(r.Goal)); } }
        public void ToggleGroupForTest(bool done){list.Controls.OfType<Button>().ElementAt(done?1:0).PerformClick();}
        protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
    }
}
