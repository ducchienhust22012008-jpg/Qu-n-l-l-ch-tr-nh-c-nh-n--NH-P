using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Nhip {
    public class DeadlinePage : Panel {
        readonly Store store;
        readonly FlowLayoutPanel flow;
        FlowLayoutPanel contentTarget;
        string selected;
        readonly Dictionary<string,Button> taskMenus=new Dictionary<string,Button>();
        int tab; bool fitting; string statusKey;
        public Action Changed;
        public int VisibleDeadlines { get; private set; }
        public bool CompletionEnabled { get; private set; }
        public string SelectedId { get { return selected; } }
        public DeadlinePage(Store data) {
            store = data; Dock = DockStyle.Fill;
            flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 0, 4, 12) };
            Controls.Add(flow); flow.SizeChanged += (s,e) => Fit();
        }
        void Fit() {
            if(fitting)return;fitting=true;
            try { int y=-flow.AutoScrollPosition.Y;flow.AutoScroll=false;flow.AutoScrollMinSize=Size.Empty;foreach(Control c in flow.Controls)c.Width=Math.Max(480,flow.ClientSize.Width-24);flow.PerformLayout();flow.AutoScroll=true;flow.PerformLayout();flow.AutoScrollPosition=new Point(0,y); }
            finally { fitting=false; }
        }
        string StatusKey() { return String.Join("|",store.Data.Deadlines.Select(d=>d.Id+":"+d.Status(DateTime.Now))); }
        public void RefreshIfStatusChanged() { if(statusKey!=StatusKey())RefreshData(); }
        FlowLayoutPanel Target { get { return contentTarget ?? flow; } }
        Label TextAt(Control parent, string text, float size, Color color, bool bold, int x, int y, int width, int height) {
            var l = Theme.Label(text, size, color, bold); l.SetBounds(x,y,width,height); l.AutoEllipsis = true; parent.Controls.Add(l); return l;
        }
        Button ButtonAt(Control parent, string text, bool primary, int x, int y, int width, Action action) {
            var b = Theme.Button(text, primary); b.SetBounds(x,y,width,38); b.Click += (s,e) => action(); parent.Controls.Add(b); return b;
        }
        void Safe(Action action) {
            try { action(); RefreshData(); if (Changed != null) Changed(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Deadline", MessageBoxButtons.OK, MessageBoxIcon.Warning); RefreshData(); }
        }
        public void ShowOverview(int filter) { selected = null; tab = filter; RefreshData(); }
        public void OpenDetail(string id) { selected = id; RefreshData(); }
        public void RefreshData() {
            if (IsDisposed) return;
            taskMenus.Clear(); contentTarget=null; flow.SuspendLayout(); foreach (Control c in flow.Controls.Cast<Control>().ToArray()) c.Dispose();
            var d = store.Data.Deadlines.FirstOrDefault(x => x.Id == selected);
            if (d == null) { selected = null; Overview(); } else Detail(d);
            flow.ResumeLayout(true); Fit(); statusKey=StatusKey();
        }
        Panel Section(int height) { var p = new Panel { Height = height, Width = 700, Margin = new Padding(0,0,0,16) }; Target.Controls.Add(p); return p; }
        static Color StateColor(Deadline d) { return d.Completed ? Theme.Green : d.Due < DateTime.Now ? Color.FromArgb(199,73,87) : d.Due <= DateTime.Now.AddDays(3) ? Color.FromArgb(185,120,35) : Theme.Purple; }
        void Overview() {
            var head = Section(96);
            var heading = TextAt(head,"Deadline của bạn",27,Theme.Ink,true,0,0,480,48);
            var desc = TextAt(head,"Chia nhỏ công việc. Giữ tiến độ. Về đích đúng hạn.",10,Theme.Muted,false,2,54,600,28);
            var add = ButtonAt(head,"+ Tạo deadline",true,520,8,158,() => EditDeadline(null));
            head.Resize += (s,e) => { add.Left = head.Width-add.Width; heading.Width = Math.Max(200,add.Left-12); desc.Width=head.Width; };
            var stats = Section(94);
            string[] names={"ĐANG LÀM","SẮP ĐẾN HẠN · 3 NGÀY","ĐÃ QUÁ HẠN"};
            int[] values={store.Data.Deadlines.Count(x=>!x.Completed),store.Data.Deadlines.Count(x=>!x.Completed && x.Due>=DateTime.Now && x.Due<=DateTime.Now.AddDays(3)),store.Data.Deadlines.Count(x=>!x.Completed && x.Due<DateTime.Now)};
            for(int i=0;i<3;i++) {
                int n=i; var card=new Card { Height=92,Padding=new Padding(0),BackColor=Theme.Tint(i==2?Color.FromArgb(199,73,87):i==1?Color.FromArgb(185,120,35):Theme.Purple,.05) }; stats.Controls.Add(card);
                TextAt(card,names[i],9,Theme.Muted,true,18,10,240,23);
                TextAt(card,values[i].ToString()+" deadline",20,i==2?Color.FromArgb(199,73,87):Theme.Purple,true,18,34,240,40);
                stats.Resize += (s,e)=>card.SetBounds(n*(stats.Width+12)/3,0,(stats.Width-24)/3,92);
            }
            var tabs=Section(42);
            string[] labels={"Đang làm","Quá hạn","Đã hoàn tất"};
            for(int i=0;i<3;i++){int n=i; ButtonAt(tabs,labels[i],tab==i,i*160,0,150,()=>ShowOverview(n));}
            var items=store.Data.Deadlines.Where(x=>tab==2?x.Completed:!x.Completed && (tab!=1 || x.Due<DateTime.Now)).OrderBy(x=>x.Due).ToList();
            VisibleDeadlines=items.Count;
            if(items.Count==0){var empty=Section(180);TextAt(empty,tab==2?"Chưa có deadline hoàn tất":"Bạn chưa có deadline trong mục này",18,Theme.Ink,true,20,28,650,40);TextAt(empty,"Tạo deadline, thêm việc nhỏ rồi đưa từng việc vào lịch khi sẵn sàng.",11,Theme.Muted,false,20,80,700,40);}
            foreach(var d in items) {
                Color color=StateColor(d);var card=new Card { Height=158,Width=700,Padding=new Padding(0),Margin=new Padding(0,0,0,14),BackColor=Theme.Tint(color,.055) };flow.Controls.Add(card);
                int total=d.TaskIds.Count,done=store.DeadlineTasks(d).Count(t=>t.Done);
                var icon=new PillLabel {Text=d.Completed?"✓":"◷",Font=Theme.Font(22,true),ForeColor=color,BackColor=Theme.Tint(color,.13),TextAlign=ContentAlignment.MiddleCenter};icon.SetBounds(18,20,50,50);card.Controls.Add(icon);
                var name=TextAt(card,d.Title,16,Theme.Ink,true,84,16,430,34);
                string remaining=d.Completed?"Đã hoàn tất":d.Due<DateTime.Now?"Quá hạn "+Math.Max(1,(DateTime.Today-d.Due.Date).Days)+" ngày":d.Due.Date==DateTime.Today?"Đến hạn hôm nay":"Còn "+(d.Due.Date-DateTime.Today).Days+" ngày";
                var state=new PillLabel {Text=remaining,Font=Theme.Font(9,true),ForeColor=color,BackColor=Theme.Tint(color,.12),TextAlign=ContentAlignment.MiddleCenter};state.SetBounds(84,60,150,28);card.Controls.Add(state);
                var due=TextAt(card,"Hạn "+d.Due.ToString("dd/MM/yyyy")+(String.IsNullOrEmpty(d.DueTime)?" · cuối ngày":" · "+d.DueTime),9.5f,Theme.Muted,false,248,60,350,28);
                var progress=TextAt(card,done+" / "+total+" việc hoàn thành",9,Theme.Muted,false,84,111,235,26);
                var bar=new PlanningProgress { Completed=done, Count=total,FillColor=color,BackColor=card.BackColor };card.Controls.Add(bar);
                var open=ButtonAt(card,"Xem chi tiết  →",false,520,19,150,()=>OpenDetail(d.Id));open.BackColor=card.BackColor;open.ForeColor=color;open.FlatAppearance.BorderSize=0;
                card.Resize+=(o,e)=>{open.Left=card.Width-166;name.Width=Math.Max(130,open.Left-100);due.Width=Math.Max(100,card.Width-268);progress.Width=210;bar.SetBounds(312,117,Math.Max(120,card.Width-334),14);};
                name.Cursor=Cursors.Hand;name.Click+=(o,e)=>OpenDetail(d.Id);
            }
            var note=Section(42);TextAt(note,"Việc nhỏ dùng phần thưởng hiện có. Deadline không cộng thêm phút thưởng.",9,Theme.Muted,false,2,4,850,30);
        }
        void Detail(Deadline d) {
            var head=Section(138);
            ButtonAt(head,"‹  Tất cả deadline",false,0,0,170,()=>ShowOverview(tab));
            var name=TextAt(head,d.Title,25,Theme.Ink,true,0,48,650,48);
            TextAt(head,"Hạn: "+d.Due.ToString("dd/MM/yyyy")+(String.IsNullOrEmpty(d.DueTime)?" · cuối ngày":" · "+d.DueTime)+"   •   "+d.Status(DateTime.Now),11,StateColor(d),true,2,105,750,30);
            var edit=ButtonAt(head,"Sửa deadline",false,520,53,145,()=>EditDeadline(d));
            head.Resize+=(s,e)=>{edit.Left=head.Width-145;name.Width=edit.Left-18;};
            var progress=new Card { Height=106,Width=700,Padding=new Padding(0),Margin=new Padding(0,0,0,16) };flow.Controls.Add(progress);
            int total=d.TaskIds.Count,done=store.DeadlineTasks(d).Count(t=>t.Done);
            var caption=TextAt(progress,"Tiến độ   "+done+" / "+total+" việc hoàn thành",13,Theme.Ink,true,20,12,500,30);
            var percent=TextAt(progress,(total==0?0:done*100/total)+"%",19,Theme.Purple,true,550,8,100,40);
            var bar=new PlanningProgress { Completed=done, Count=total };progress.Controls.Add(bar);
            TextAt(progress,total>0 && done==total?"Tất cả việc đã xong. Xác nhận đã nộp / bàn giao ở bên dưới.":"Mỗi việc nhỏ có trọng số bằng nhau. Xong việc chưa tự động hoàn tất deadline.",9,Theme.Muted,false,20,71,850,26);
            progress.Resize+=(s,e)=>{bar.SetBounds(20,53,progress.Width-40,12);percent.Left=progress.Width-100;caption.Width=progress.Width-135;};
            var split=Section(Math.Max(500,total*92+60));
            var left=new FlowLayoutPanel { FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=false,Margin=new Padding(0) };split.Controls.Add(left);
            var aside=new Card { Padding=new Padding(0),Height=460 };split.Controls.Add(aside);
            split.Resize+=(s,e)=>{aside.SetBounds(split.Width-294,0,294,460);left.SetBounds(0,0,split.Width-310,split.Height);foreach(Control c in left.Controls)c.Width=Math.Max(200,left.Width);};
            contentTarget=left;
            var toolbar=Section(44);TextAt(toolbar,"VIỆC NHỎ",9,Theme.Ink,true,2,4,110,32);
            var add=ButtonAt(toolbar,"+ Thêm việc",true,200,0,130,()=>EditPending(d,null));
            var link=ButtonAt(toolbar,"Gắn việc có sẵn",false,340,0,160,()=>Attach(d));
            add.Enabled=link.Enabled=!d.Completed;
            toolbar.Resize+=(s,e)=>{link.Left=toolbar.Width-160;add.Left=link.Left-140;};
            if(total==0){var empty=Section(90);TextAt(empty,"Chưa có việc nhỏ. Thêm việc hoặc gắn công việc có sẵn để bắt đầu.",11,Theme.Muted,false,18,16,450,55);}
            foreach(var t in store.DeadlineTasks(d)) AddTaskRow(d,t);
            contentTarget=null;
            TextAt(aside,"THÔNG TIN DEADLINE",10,Theme.Purple,true,18,14,260,28);
            TextAt(aside,"Hạn bàn giao",9,Theme.Muted,false,18,57,258,24);
            TextAt(aside,d.Due.ToString("dd/MM/yyyy")+" · "+(String.IsNullOrEmpty(d.DueTime)?"cuối ngày":d.DueTime),12,Theme.Ink,true,18,82,258,30);
            TextAt(aside,"GHI CHÚ",9,Theme.Muted,true,18,134,258,25);
            var text=new TextBox { Text=String.IsNullOrWhiteSpace(d.Notes)?"Chưa có ghi chú. Chọn Sửa deadline để bổ sung.":d.Notes,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,Font=Theme.Font(10) };text.SetBounds(18,169,258,106);aside.Controls.Add(text);
            CompletionEnabled=d.Completed || (total>0 && done==total);
            var complete=ButtonAt(aside,d.Completed?"Mở lại deadline":"✓  Đã nộp / bàn giao",true,18,293,258,()=>Safe(()=>store.CompleteDeadline(d.Id,!d.Completed)));complete.Enabled=CompletionEnabled;if(!CompletionEnabled)complete.BackColor=Theme.Line;
            TextAt(aside,d.Completed?"Đã xác nhận: "+DateTime.Parse(d.CompletedAt,null,DateTimeStyles.RoundtripKind).ToLocalTime().ToString("dd/MM/yyyy HH:mm"):"Hoàn thành tất cả việc để xác nhận.\nDeadline không cộng thưởng thêm.",9,Theme.Muted,false,18,341,258,48);
            ButtonAt(aside,"Xóa deadline",false,18,402,258,()=>{if(MessageBox.Show(this,"Xóa deadline này? Tất cả công việc và phần thưởng được giữ lại. Việc chưa xếp lịch sẽ có trong danh sách Gắn việc có sẵn.","Xóa deadline",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)Safe(()=>{store.DeleteDeadline(d.Id);selected=null;});});
        }
        void AddTaskRow(Deadline d, TaskItem t) {
            bool scheduled=store.IsScheduled(t.Id);bool late=scheduled && DateTime.ParseExact(t.Day+" "+t.Time,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture).AddMinutes(t.Duration)>d.Due;
            var row=new Card { Height=82,Width=700,Padding=new Padding(0),Margin=new Padding(0,0,0,10),BackColor=Theme.Tint(t.Done?Theme.Green:Theme.Purple,.035) };Target.Controls.Add(row);
            var check=new CheckBox { Checked=t.Done,Cursor=Cursors.Hand,Enabled=!d.Completed };check.SetBounds(18,20,24,28);row.Controls.Add(check);check.CheckedChanged+=(s,e)=>{bool value=check.Checked;BeginInvoke((Action)(()=>Safe(()=>store.SetDone(t.Id,value))));};
            var name=TextAt(row,t.Title,11,t.Done?Theme.Muted:Theme.Ink,true,54,10,400,30);
            var meta=TextAt(row,(scheduled?DateTime.ParseExact(t.Day,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("dd/MM/yyyy")+" · "+t.Time:"Chưa xếp lịch")+" · "+t.Duration+" phút · "+t.Category+" · +"+(t.Done?t.Awarded:t.Reward)+"p",9,Theme.Muted,false,54,42,480,25);
            var tooltip=new ToolTip { AutoPopDelay=15000 };tooltip.SetToolTip(name,t.Title);tooltip.SetToolTip(meta,meta.Text);row.Disposed+=(s,e)=>tooltip.Dispose();
            var badge=TextAt(row,late?"Lịch kết thúc sau hạn deadline":"",8,Color.FromArgb(199,73,87),false,54,63,430,18);
            var schedule=ButtonAt(row,scheduled?"Đổi lịch":"Đưa vào lịch",!scheduled,500,17,135,()=>Schedule(d,t));schedule.Enabled=!d.Completed;
            var menu=ButtonAt(row,"⋯",false,640,17,40,()=>{
                var m=new ContextMenuStrip();m.Items.Add("Sửa công việc",null,(s,e)=>UiMenus.AfterClick(this,()=>{if(scheduled)Schedule(d,t);else EditPending(d,t);}));
                m.Items.Add("Gỡ khỏi deadline (giữ công việc)",null,(s,e)=>UiMenus.AfterClick(this,()=>Safe(()=>store.DetachTask(d.Id,t.Id))));
                m.Items.Add("Xóa công việc",null,(s,e)=>UiMenus.AfterClick(this,()=>{if(MessageBox.Show(this,"Xóa công việc “"+t.Title+"”?"+(t.Done?"\nThu hồi "+t.Awarded+" phút đã thưởng.":""),"Xóa công việc",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)Safe(()=>store.Delete(t.Id));}));
                row.ContextMenuStrip=m;m.Closed+=(s,e)=>{if(!row.IsDisposed)row.ContextMenuStrip=null;UiMenus.DisposeAfterEvent(m);};m.Show(row,new Point(row.Width-200,60));
            });menu.Enabled=!d.Completed;taskMenus[t.Id]=menu;
            row.Resize+=(s,e)=>{menu.Left=row.Width-56;schedule.Left=menu.Left-145;name.Width=meta.Width=Math.Max(120,schedule.Left-70);badge.Width=name.Width;};
        }
        public ContextMenuStrip OpenTaskMenuForTest(string id){var button=taskMenus[id];button.PerformClick();return button.Parent.ContextMenuStrip;}
        void EditDeadline(Deadline existing) { using(var dialog=new DeadlineDialog(existing))if(dialog.ShowDialog(this)==DialogResult.OK)Safe(()=>{store.SaveDeadline(dialog.Result);selected=dialog.Result.Id;}); }
        void EditPending(Deadline parent,TaskItem existing) { using(var dialog=new DeadlineTaskDialog(existing,store.Data.Settings.DefaultReward))if(dialog.ShowDialog(this)==DialogResult.OK)Safe(()=>{if(existing==null)store.AddDeadlineTask(parent.Id,dialog.Result);else store.SavePendingTask(dialog.Result);}); }
        void Attach(Deadline d) {
            var choices=store.AllTasks.Where(t=>store.ParentDeadline(t.Id)==null).OrderBy(t=>t.Title).ToList();
            using(var dialog=new AttachTaskDialog(choices,store))if(dialog.ShowDialog(this)==DialogResult.OK){
                var t=store.AllTasks.First(x=>x.Id==dialog.TaskId);
                if(store.IsScheduled(t.Id) && DateTime.ParseExact(t.Day+" "+t.Time,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture).AddMinutes(t.Duration)>d.Due && MessageBox.Show(this,"Công việc này kết thúc sau hạn deadline. Vẫn gắn công việc?","Lịch sau hạn",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
                Safe(()=>store.AttachTask(d.Id,dialog.TaskId));
            }
        }
        void Schedule(Deadline d,TaskItem task) {
            using(var dialog=new TaskDialog(task,DateTime.Today,store.Data.Settings.DefaultReward,true,false,store))if(dialog.ShowDialog(this)==DialogResult.OK)Safe(()=>store.SaveTaskWithDeadline(dialog.Result,dialog.SelectedDeadlineId));
        }
    }
    public class DeadlineDialog : BrandedForm {
        TextBox title,notes; DateTimePicker day,time; CheckBox hasTime;
        public Deadline Result { get; private set; }
        readonly string id;
        public DeadlineDialog(Deadline d) {
            id=d==null?Guid.NewGuid().ToString("N"):d.Id;Text=d==null?"Tạo deadline":"Sửa deadline";Font=Theme.Font(10);BackColor=Theme.Bg;ClientSize=new Size(530,530);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
            AddLabel("Một đích đến, từng bước nhỏ",21,true,22,15,490,40);
            AddLabel("Tên deadline *",10,true,24,72,460,25);title=new TextBox { Text=d==null?"":d.Title,MaxLength=160 };title.SetBounds(24,101,480,30);Controls.Add(title);
            AddLabel("Ngày đến hạn *",10,true,24,149,240,25);day=new DateTimePicker { Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy",Value=d==null?DateTime.Today.AddDays(7):DateTime.ParseExact(d.DueDay,"yyyy-MM-dd",CultureInfo.InvariantCulture) };day.SetBounds(24,180,225,30);Controls.Add(day);
            hasTime=new CheckBox { Text="Chọn giờ cụ thể",Checked=d!=null&&!String.IsNullOrEmpty(d.DueTime) };hasTime.SetBounds(274,149,230,26);Controls.Add(hasTime);
            time=new DateTimePicker { Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true,Value=DateTime.Today.Add(String.IsNullOrEmpty(d==null?null:d.DueTime)?new TimeSpan(23,59,0):TimeSpan.Parse(d.DueTime)),Enabled=hasTime.Checked };time.SetBounds(274,180,230,30);Controls.Add(time);hasTime.CheckedChanged+=(s,e)=>time.Enabled=hasTime.Checked;
            AddLabel("Không chọn giờ: hạn được tính đến cuối ngày.",9,false,24,218,480,26);
            AddLabel("Ghi chú",10,true,24,260,400,25);notes=new TextBox { Multiline=true,ScrollBars=ScrollBars.Vertical,MaxLength=4000,Text=d==null?"":d.Notes };notes.SetBounds(24,291,480,130);Controls.Add(notes);
            var cancel=Theme.Button("Hủy");cancel.SetBounds(248,465,115,40);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);CancelButton=cancel;
            var save=Theme.Button("Lưu deadline",true);save.SetBounds(375,465,129,40);save.Click+=(s,e)=>{if(String.IsNullOrWhiteSpace(title.Text)){MessageBox.Show(this,"Nhập tên deadline.");title.Focus();return;}Result=new Deadline { Id=id,Title=title.Text.Trim(),DueDay=day.Value.ToString("yyyy-MM-dd"),DueTime=hasTime.Checked?time.Value.ToString("HH:mm"):null,Notes=notes.Text.Trim() };DialogResult=DialogResult.OK;};Controls.Add(save);AcceptButton=save;
        }
        void AddLabel(string text,float size,bool bold,int x,int y,int w,int h){var l=Theme.Label(text,size,bold?Theme.Ink:Theme.Muted,bold);l.SetBounds(x,y,w,h);Controls.Add(l);}
        public void FillForTest(string name) { title.Text=name; }
    }
    public class DeadlineTaskDialog : BrandedForm {
        TextBox title; NumericUpDown duration,reward;ComboBox category;TaskItem original;
        public TaskItem Result { get; private set; }
        public DeadlineTaskDialog(TaskItem t,int defaultReward){
            original=t;Text=t==null?"Thêm việc nhỏ":"Sửa việc nhỏ";Font=Theme.Font(10);BackColor=Theme.Bg;ClientSize=new Size(500,360);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
            var heading=Theme.Label("Bắt đầu bằng một việc cụ thể",18,Theme.Ink,true);heading.SetBounds(22,12,460,40);Controls.Add(heading);
            var l=Theme.Label("Tên công việc",10,Theme.Muted);l.SetBounds(24,65,450,25);Controls.Add(l);title=new TextBox{Text=t==null?"":t.Title,MaxLength=160};title.SetBounds(24,95,450,30);Controls.Add(title);
            l=Theme.Label("Thời lượng (phút)                 Phần thưởng (phút)",10,Theme.Muted);l.SetBounds(24,141,450,25);Controls.Add(l);
            duration=new NumericUpDown{Minimum=1,Maximum=1440,Value=t==null?60:t.Duration};duration.SetBounds(24,171,207,30);Controls.Add(duration);reward=new NumericUpDown{Minimum=0,Maximum=600,Value=t==null?defaultReward:t.Reward};reward.SetBounds(262,171,212,30);Controls.Add(reward);
            category=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};category.Items.AddRange(new object[]{"Học tập","Công việc","Vận động","Cá nhân"});category.SelectedItem=t==null?"Học tập":t.Category;if(category.SelectedIndex<0)category.SelectedIndex=0;category.SetBounds(24,220,207,30);Controls.Add(category);
            l=Theme.Label("Chưa xếp lịch · Có thể đưa vào lịch sau.",9,Theme.Muted);l.SetBounds(24,263,450,28);Controls.Add(l);
            var cancel=Theme.Button("Hủy");cancel.SetBounds(224,310,110,36);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);CancelButton=cancel;
            var save=Theme.Button("Lưu việc",true);save.SetBounds(346,310,128,36);save.Click+=(s,e)=>{if(String.IsNullOrWhiteSpace(title.Text)){MessageBox.Show(this,"Nhập tên công việc.");return;}Result=new TaskItem{Id=original==null?Guid.NewGuid().ToString("N"):original.Id,Title=title.Text.Trim(),Day=original==null?DateTime.Today.ToString("yyyy-MM-dd"):original.Day,Time=original==null?"09:00":original.Time,Duration=(int)duration.Value,Reward=(int)reward.Value,Category=Convert.ToString(category.SelectedItem),ColorHex=original==null?null:original.ColorHex};DialogResult=DialogResult.OK;};Controls.Add(save);AcceptButton=save;
        }
        public void FillForTest(string name){title.Text=name;}
    }
    public class AttachTaskDialog : BrandedForm {
        public string TaskId { get; private set; }
        public AttachTaskDialog(List<TaskItem> items,Store store){
            Text="Gắn việc có sẵn";Font=Theme.Font(10);BackColor=Theme.Bg;ClientSize=new Size(650,430);StartPosition=FormStartPosition.CenterParent;MinimumSize=Size;
            var caption=Theme.Label("Chọn công việc chưa thuộc deadline nào",14,Theme.Ink,true);caption.SetBounds(20,16,610,38);Controls.Add(caption);
            var list=new ListBox { IntegralHeight=false,HorizontalScrollbar=true };list.SetBounds(20,65,610,285);list.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top|AnchorStyles.Bottom;foreach(var t in items)list.Items.Add(t.Title+"  ·  "+(store.IsScheduled(t.Id)?t.Day+" "+t.Time:"Chưa xếp lịch")+(t.Done?"  ✓":""));Controls.Add(list);
            var cancel=Theme.Button("Hủy");cancel.SetBounds(360,372,120,38);cancel.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);CancelButton=cancel;
            var save=Theme.Button("Gắn công việc",true);save.SetBounds(490,372,140,38);save.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;save.Enabled=items.Count>0;save.Click+=(s,e)=>{if(list.SelectedIndex<0)return;TaskId=items[list.SelectedIndex].Id;DialogResult=DialogResult.OK;};Controls.Add(save);AcceptButton=save;if(items.Count>0)list.SelectedIndex=0;else caption.Text="Không có công việc chưa gắn deadline.";
        }
    }
}
