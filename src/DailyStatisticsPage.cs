using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Nhip {
    sealed class DailyActivityChart : Control {
        public DailyReport Report;
        readonly ToolTip tip=new ToolTip {AutoPopDelay=15000};
        readonly List<KeyValuePair<Rectangle,DailyActivity>> areas=new List<KeyValuePair<Rectangle,DailyActivity>>();
        public DailyActivityChart(){DoubleBuffered=true;BackColor=Color.White;AccessibleName="Hoạt động theo giờ trong ngày";}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;areas.Clear();
            using(var title=Theme.Font(11,true))using(var font=Theme.Font(8.5f)){
                TextRenderer.DrawText(g,"Hoạt động theo giờ",title,new Rectangle(16,10,Width-32,28),Theme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g,"Thời gian thực tế đã ghi nhận · Tạm dừng không được tính",font,new Rectangle(16,40,Width-32,23),Theme.Muted,TextFormatFlags.EndEllipsis);
                int x=86,w=Math.Max(40,Width-x-18),top=96;int step=Width<650?4:3;
                for(int hour=0;hour<=24;hour+=step){int px=x+(int)(w*hour/24.0);using(var pen=new Pen(Theme.Line)){pen.DashStyle=DashStyle.Dot;g.DrawLine(pen,px,top-6,px,top+84);}TextRenderer.DrawText(g,hour.ToString("00")+":00",font,new Rectangle(px-24,top-31,48,22),Theme.Muted,TextFormatFlags.HorizontalCenter);}
                for(int lane=0;lane<2;lane++){var r=new Rectangle(x,top+lane*44,w,34);using(var path=Theme.Rounded(r,6))using(var brush=new SolidBrush(Theme.Tint(lane==0?Theme.Purple:Color.FromArgb(197,132,28),.045)))g.FillPath(brush,path);TextRenderer.DrawText(g,lane==0?"Tập trung":"Giải trí",font,new Rectangle(10,r.Top,72,34),Theme.Ink,TextFormatFlags.VerticalCenter);}
                if(Report!=null)foreach(var item in Report.Activities){
                    double begin=Math.Max(0,(item.Start-Report.Day).TotalHours),end=Math.Min(24,(item.End-Report.Day).TotalHours);
                    int left=x+(int)(w*begin/24),right=x+(int)(w*end/24);int width=Math.Min(Math.Max(3,right-left),Math.Max(1,x+w-left));
                    var r=new Rectangle(left,top+(item.Leisure?44:0)+3,width,28);Color color=item.Leisure?Color.FromArgb(217,148,37):item.Category=="Công việc"?StatisticsBars.WorkColor:Theme.Purple;
                    using(var path=Theme.Rounded(r,Math.Min(5,Math.Max(1,width/2))))using(var brush=new SolidBrush(color))g.FillPath(brush,path);
                    if(width>=44)TextRenderer.DrawText(g,DailyStatisticsPage.DayTime(item.Start,Report.Day)+(width>110?"–"+DailyStatisticsPage.DayTime(item.End,Report.Day):""),font,r,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
                    areas.Add(new KeyValuePair<Rectangle,DailyActivity>(r,item));
                }
                TextRenderer.DrawText(g,"● Học tập",font,new Rectangle(16,Height-28,95,22),Theme.Purple);
                TextRenderer.DrawText(g,"● Công việc",font,new Rectangle(112,Height-28,105,22),StatisticsBars.WorkColor);
                TextRenderer.DrawText(g,"● Giải trí",font,new Rectangle(220,Height-28,95,22),Color.FromArgb(197,132,28));
                if(Report!=null && Report.Activities.Count==0)TextRenderer.DrawText(g,"Chưa có thời gian được ghi nhận trong ngày này",font,new Rectangle(x,top+4,w,28),Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);var hit=areas.LastOrDefault(a=>a.Key.Contains(e.Location));var item=hit.Value;tip.SetToolTip(this,item==null?"":item.Title+"\n"+DailyStatisticsPage.DayTime(item.Start,Report.Day)+"–"+DailyStatisticsPage.DayTime(item.End,Report.Day)+" · "+FocusPage.Duration(item.Seconds)+(item.Approximate?"\nGiải trí cũ được phân bổ gần đúng theo mốc lưu.":""));}
        protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
    }
    sealed class DailyStatisticsPage : UserControl {
        readonly Store store;
        readonly Panel scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true},content=new Panel();
        readonly Label heading=Theme.Label("Thống kê trong ngày",25,Theme.Ink,true),subtitle=Theme.Label("",10,Theme.Muted),note=Theme.Label("Từ 00:00 đến 24:00 theo giờ trên máy · Phiên tạm dừng không được tính",8.5f,Theme.Muted),details=Theme.Label("",11,Theme.Ink,true),footer=Theme.Label("",8.5f,Theme.Muted);
        readonly Card filter=new Card(),chartCard=new Card(),tableCard=new Card();
        readonly DateTimePicker date=new DateTimePicker();
        readonly List<Button> modes=new List<Button>(),tabs=new List<Button>();
        readonly Button previous=Theme.Button("‹"),next=Theme.Button("›"),today=Theme.Button("Hôm nay"),yesterday=Theme.Button("Hôm qua"),reload=Theme.Button("↻");
        readonly StatisticsMetric focus=new StatisticsMetric(),tasks=new StatisticsMetric(),rewards=new StatisticsMetric(),leisure=new StatisticsMetric();
        readonly DailyActivityChart chart=new DailyActivityChart();
        readonly DataGridView grid=Grids.Create();
        readonly Label empty=Theme.Label("",10,Theme.Muted);
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer {Interval=15000};
        readonly Font durationFont=new Font("Consolas",10);
        DailyReport report;bool arranging,settingDate;int selectedTab;
        public event Action<string> RangeRequested;
        public DailyStatisticsPage(Store source){
            store=source;Dock=DockStyle.Fill;BackColor=Theme.Bg;scroll.BackColor=content.BackColor=Theme.Bg;Controls.Add(scroll);scroll.Controls.Add(content);
            content.Controls.Add(heading);content.Controls.Add(subtitle);content.Controls.Add(filter);content.Controls.Add(note);
            foreach(string value in new[]{"Một ngày","7 ngày","30 ngày","12 tháng","Tùy chọn"}){string mode=value;var button=Theme.Button(value,value=="Một ngày");button.Font=Theme.Font(9,true);button.Click+=(s,e)=>{if(mode=="Một ngày")RefreshData();else if(RangeRequested!=null)RangeRequested(mode);};filter.Controls.Add(button);modes.Add(button);}
            date.Format=DateTimePickerFormat.Custom;date.CustomFormat="dd/MM/yyyy";date.Font=Theme.Font(10);date.Value=DateTime.Today;date.AccessibleName="Ngày cần xem thống kê";filter.Controls.Add(date);date.ValueChanged+=(s,e)=>{if(!settingDate)RefreshData();};
            foreach(var b in new[]{previous,next,today,yesterday,reload})filter.Controls.Add(b);
            previous.AccessibleName="Thống kê ngày trước";next.AccessibleName="Thống kê ngày sau";reload.AccessibleName="Làm mới thống kê ngày";
            previous.Click+=(s,e)=>MoveDay(-1);next.Click+=(s,e)=>MoveDay(1);today.Click+=(s,e)=>SelectDay(store.Clock().ToLocalTime().Date);yesterday.Click+=(s,e)=>SelectDay(store.Clock().ToLocalTime().Date.AddDays(-1));reload.Click+=(s,e)=>RefreshData();
            focus.Caption="TẬP TRUNG";focus.ValueColor=Theme.Purple;tasks.Caption="VIỆC HOÀN THÀNH";tasks.ValueColor=Theme.Green;rewards.Caption="PHÚT THƯỞNG";rewards.ValueColor=Theme.Green;leisure.Caption="GIẢI TRÍ ĐÃ DÙNG";leisure.ValueColor=Color.FromArgb(185,120,35);
            foreach(var metric in new[]{focus,tasks,rewards,leisure})content.Controls.Add(metric);
            chartCard.Padding=new Padding(2);chart.Dock=DockStyle.Fill;chartCard.Controls.Add(chart);content.Controls.Add(chartCard);content.Controls.Add(details);
            foreach(string label in new[]{"Phiên tập trung","Công việc","Ví thời gian"}){int n=tabs.Count;var b=Theme.Button(label);b.Font=Theme.Font(9,true);b.Click+=(s,e)=>{selectedTab=n;RenderTable();};content.Controls.Add(b);tabs.Add(b);}
            grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=grid.AllowUserToDeleteRows=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.RowTemplate.Height=42;grid.Font=Theme.Font(9);grid.ColumnHeadersHeight=36;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(250,249,255);
            tableCard.Padding=new Padding(1);tableCard.Controls.Add(grid);content.Controls.Add(tableCard);empty.Dock=DockStyle.Fill;empty.TextAlign=ContentAlignment.MiddleCenter;tableCard.Controls.Add(empty);content.Controls.Add(footer);
            scroll.Resize+=(s,e)=>Arrange();VisibleChanged+=(s,e)=>{if(Visible)Arrange();};timer.Tick+=(s,e)=>{if(Visible && FindForm()!=null && FindForm().Visible && date.Value.Date==store.Clock().ToLocalTime().Date)RefreshData();};timer.Start();RefreshData();
        }
        public static string DayTime(DateTime value,DateTime day){return value.Date>day.Date?"24:00":value.ToString("HH:mm");}
        void MoveDay(int amount){var value=date.Value.Date.AddDays(amount);if(value>=date.MinDate.Date && value<=date.MaxDate.Date)SelectDay(value);}
        public void SelectDay(DateTime value){settingDate=true;try{date.Value=value.Date;}finally{settingDate=false;}RefreshData();}
        public void RefreshData(){
            try{
                report=store.ReportForDay(date.Value.Date);var b=report.Summary;
                subtitle.Text=date.Value.ToString("dddd, dd/MM/yyyy",CultureInfo.GetCultureInfo("vi-VN"));
                focus.Value=StatisticsPage.TimeText(b.FocusSeconds);focus.Detail=report.Focus.Count(r=>!r.Draft)+" phiên đã lưu"+(report.Focus.Any(r=>r.Draft)?" · có phiên chưa lưu":"");
                tasks.Value=b.Completed+" / "+b.Planned;tasks.Fraction=b.Planned>0?(double?)b.Completed/b.Planned:null;tasks.Detail=b.Planned>0?(100.0*b.Completed/b.Planned).ToString("0")+"% · Theo lịch ngày đã chọn":"Chưa có công việc trong lịch";
                rewards.Value="+"+b.EarnedMinutes.ToString("0.#")+" phút";rewards.Detail="Thu hồi "+b.RevokedMinutes.ToString("0.#")+"p · Chênh lệch "+(b.EarnedMinutes-b.RevokedMinutes).ToString("+0.#;-0.#;0")+"p";
                leisure.Value=StatisticsPage.TimeText(b.EntertainmentMinutes*60);leisure.Detail="Trong ngày đã chọn";
                foreach(var metric in new[]{focus,tasks,rewards,leisure})metric.Invalidate();chart.Report=report;chart.Invalidate();
                previous.Enabled=date.Value.Date>date.MinDate.Date;next.Enabled=date.Value.Date<date.MaxDate.Date;RenderTable();Arrange();
            }catch(Exception ex){MessageBox.Show(this,ex.Message,"Thống kê ngày",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        void Column(string id,string caption,int weight,bool number){Grids.Column(grid,id,caption,weight,true);if(number){grid.Columns[id].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;grid.Columns[id].HeaderCell.Style.Alignment=DataGridViewContentAlignment.MiddleRight;}}
        void RenderTable(){
            if(report==null)return;grid.Rows.Clear();grid.Columns.Clear();
            tabs[0].Text="Phiên tập trung ("+report.Focus.Count+")";tabs[1].Text="Công việc ("+report.Tasks.Count+")";
            for(int i=0;i<tabs.Count;i++){tabs[i].BackColor=i==selectedTab?Theme.Sidebar:Color.White;tabs[i].ForeColor=i==selectedTab?Theme.Purple:Theme.Muted;}
            details.Text="Chi tiết ngày "+date.Value.ToString("dd/MM/yyyy");
            if(selectedTab==0){
                Column("start","Bắt đầu",16,false);Column("end","Kết thúc",16,false);Column("title","Phiên tập trung",46,false);Column("kind","Loại",20,false);Column("seconds","Thời gian thực",26,true);
                foreach(var r in report.Focus){int i=grid.Rows.Add(DayTime(r.Start,report.Day),DayTime(r.End,report.Day),r.Title+(r.Draft?(r.Running?" · đang chạy":" · chưa lưu"):""),r.Category,FocusPage.Duration(r.Seconds));grid.Rows[i].Cells["title"].ToolTipText=r.Title;grid.Rows[i].Cells["seconds"].ToolTipText="Đã loại thời gian tạm dừng; chỉ tính phần thuộc ngày đã chọn.";grid.Rows[i].Cells["kind"].Style.ForeColor=r.Category=="Học tập"?Theme.Purple:StatisticsBars.WorkColor;}
                grid.Columns["seconds"].DefaultCellStyle.Font=durationFont;grid.Columns["seconds"].DefaultCellStyle.ForeColor=Theme.Purple;
                empty.Text="Chưa có phiên tập trung trong ngày này.\nCác phiên chạy qua nửa đêm được chia vào đúng từng ngày.";
                footer.Text="Tổng cộng: "+FocusPage.Duration(report.Summary.FocusSeconds)+" · "+report.Focus.Count+" phiên · Đã loại thời gian tạm dừng.";
            }else if(selectedTab==1){
                Column("time","Giờ",14,false);Column("title","Công việc",48,false);Column("category","Nhóm",20,false);Column("duration","Lịch (phút)",20,true);Column("status","Trạng thái",25,false);
                foreach(var t in report.Tasks){var p=store.ParentDeadline(t.Id);int i=grid.Rows.Add(t.Time,t.Title,t.Category,t.Duration,t.Done?"Đã hoàn thành":"Chưa hoàn thành");grid.Rows[i].Cells["status"].Style.ForeColor=t.Done?Theme.Green:Theme.Muted;grid.Rows[i].Cells["title"].ToolTipText=t.Title+(p==null?"":"\nDeadline: "+p.Title);}
                empty.Text="Chưa có công việc được xếp lịch vào ngày này.";
                footer.Text="Công việc theo ngày trong lịch; dấu hoàn thành phản ánh trạng thái hiện tại.\nThời lượng trong lịch là dự kiến, không phải giờ bấm thực tế.";
            }else{
                Column("start","Thời điểm",20,false);Column("end","Đến",14,false);Column("kind","Loại",20,false);Column("detail","Nội dung",54,false);Column("minutes","Thay đổi (phút)",25,true);
                foreach(var r in report.Wallet){int i=grid.Rows.Add(DayTime(r.Start,report.Day),r.Kind=="Giải trí"?DayTime(r.End,report.Day):"—",r.Kind,r.Detail,(r.Seconds/60).ToString("+0.##;-0.##;0"));grid.Rows[i].Cells["minutes"].Style.ForeColor=r.Seconds>0?Theme.Green:r.Kind=="Giải trí"?Color.FromArgb(185,120,35):Color.FromArgb(190,65,83);grid.Rows[i].Cells["detail"].ToolTipText=r.Detail;}
                empty.Text="Chưa có giao dịch ví thời gian trong ngày này.";
                footer.Text="Thưởng +"+report.Summary.EarnedMinutes.ToString("0.#")+"p · Thu hồi "+report.Summary.RevokedMinutes.ToString("0.#")+"p · Giải trí "+report.Summary.EntertainmentMinutes.ToString("0.#")+"p.\nGiải trí từ bản ghi cũ được phân bổ gần đúng theo mốc lưu; đây không phải số dư ví hiện tại.";
            }
            grid.ClearSelection();empty.Visible=grid.Rows.Count==0;grid.Visible=!empty.Visible;empty.BringToFront();
        }
        void Arrange(){
            if(arranging)return;arranging=true;
            try{
                int old=-scroll.AutoScrollPosition.Y;scroll.AutoScroll=false;content.Location=Point.Empty;
                int w=Math.Max(490,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-3);content.Width=w;
                heading.SetBounds(0,0,w,46);subtitle.SetBounds(2,47,w-4,28);filter.SetBounds(0,84,w,90);
                int modeX=10;int[] modeWidths={92,68,74,82,82};for(int i=0;i<modes.Count;i++){modes[i].SetBounds(modeX,8,modeWidths[i],32);modeX+=modeWidths[i]+6;}
                previous.SetBounds(12,49,30,29);date.SetBounds(50,49,155,29);next.SetBounds(213,49,30,29);reload.SetBounds(251,49,32,29);today.SetBounds(w-210,48,94,31);yesterday.SetBounds(w-106,48,94,31);
                note.SetBounds(2,180,w-4,28);int columns=w<760?2:4,mw=(w-(columns-1)*10)/columns,y=216;var metrics=new[]{focus,tasks,rewards,leisure};for(int i=0;i<metrics.Length;i++)metrics[i].SetBounds((i%columns)*(mw+10),y+(i/columns)*120,mw,110);y+=columns==2?240:120;
                chartCard.SetBounds(0,y,w,224);y+=238;details.SetBounds(2,y,w-4,30);y+=35;
                tabs[0].SetBounds(0,y,180,34);tabs[1].SetBounds(188,y,150,34);tabs[2].SetBounds(346,y,140,34);y+=44;
                tableCard.SetBounds(0,y,w,210);y+=220;footer.SetBounds(2,y,w-4,48);content.Height=y+56;
                scroll.AutoScrollMinSize=new Size(0,content.Height);scroll.AutoScroll=true;scroll.PerformLayout();scroll.AutoScrollPosition=new Point(0,old);
            }finally{arranging=false;}
        }
        public void TabForTest(int tab){tabs[tab].PerformClick();}
        public int RowsForTest {get{return grid.Rows.Count;}}
        public DailyReport ReportForTest {get{return report;}}
        public bool FitsForTest {get{return !scroll.HorizontalScroll.Visible && content.Controls.Cast<Control>().All(c=>c.Right<=content.Width) && filter.Controls.Cast<Control>().All(c=>c.Right<=filter.ClientSize.Width);}}
        public void MoveForTest(int days){MoveDay(days);}
        public void TodayForTest(){today.PerformClick();}
        public void YesterdayForTest(){yesterday.PerformClick();}
        public void RangeForTest(string mode){if(RangeRequested!=null)RangeRequested(mode);}
        public void ScrollDetailsForTest(){scroll.AutoScrollPosition=new Point(0,Math.Max(0,details.Top-8));}
        protected override void Dispose(bool disposing){if(disposing){timer.Dispose();durationFont.Dispose();}base.Dispose(disposing);}
    }
}
