using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Nhip {
    sealed class DeadlineSchedulePanel : Panel {
        readonly Store store;
        readonly ComboBox deadlines=new ComboBox(),tasks=new ComboBox();
        readonly DateTimePicker day=new DateTimePicker(),time=new DateTimePicker();
        readonly NumericUpDown duration=new NumericUpDown {Minimum=1,Maximum=1440,Value=60};
        readonly Button pending,all;
        readonly Label due,summary,endTime,empty;
        readonly Card info;
        bool onlyPending=true,binding;
        public readonly Button SaveButton,CancelAction;
        public event Action Prepared;
        public TaskItem Result {get;private set;}
        public bool RescheduleConfirmed {get;private set;}
        Func<bool> confirmationForTest;
        class Choice {public string Id,Caption;public override string ToString(){return Caption;}}
        public string SelectedDeadlineId {get{var c=deadlines.SelectedItem as Choice;return c==null?null:c.Id;}}
        string SelectedTaskId {get{var c=tasks.SelectedItem as Choice;return c==null?null:c.Id;}}
        public DeadlineSchedulePanel(Store source,DateTime selected,bool precise){
            store=source;BackColor=Theme.Bg;Size=new Size(620,566);
            Section("1","Chọn Deadline",0);SetupCombo(deadlines,36);deadlines.AccessibleName="Chọn deadline trước";
            deadlines.Items.Add(new Choice {Caption="Chọn deadline…"});foreach(var d in store.Data.Deadlines.Where(d=>!d.Completed).OrderBy(d=>d.Due))deadlines.Items.Add(new Choice {Id=d.Id,Caption=d.Title});deadlines.SelectedIndex=0;
            due=LabelAt("",8.5f,Theme.Muted,28,71,564,23);
            Section("2","Chọn công việc",101);
            pending=Theme.Button("Chưa xếp lịch",true);pending.SetBounds(28,138,180,30);pending.Font=Theme.Font(9,true);pending.Click+=(s,e)=>SetFilter(true);Controls.Add(pending);
            all=Theme.Button("Tất cả việc chưa xong");all.SetBounds(218,138,255,30);all.Font=Theme.Font(9,true);all.Click+=(s,e)=>SetFilter(false);Controls.Add(all);
            SetupCombo(tasks,178);tasks.AccessibleName="Chọn việc trong deadline";
            info=new Card {Padding=new Padding(0),BackColor=Theme.Sidebar};info.SetBounds(28,224,564,72);Controls.Add(info);
            summary=Theme.Label("Chọn deadline trước, sau đó chọn công việc.",9.5f,Theme.Ink);summary.SetBounds(12,9,540,54);summary.AutoEllipsis=true;info.Controls.Add(summary);
            empty=LabelAt("",8.5f,Theme.Muted,28,204,564,20);
            Section("3","Đặt lịch",311);
            LabelAt("Ngày thực hiện",9.5f,Theme.Muted,28,347,238,24);LabelAt("Giờ bắt đầu",9.5f,Theme.Muted,282,347,142,24);LabelAt("Thời lượng (phút)",9.5f,Theme.Muted,440,347,152,24);
            day.Format=DateTimePickerFormat.Custom;day.CustomFormat="dd/MM/yyyy";day.Value=selected.Date;day.SetBounds(28,375,238,30);day.Font=Theme.Font(10.5f);Controls.Add(day);
            time.Format=DateTimePickerFormat.Custom;time.CustomFormat="HH:mm";time.ShowUpDown=true;time.Value=DateTime.Today.Add(!precise && selected.TimeOfDay==TimeSpan.Zero?TimeSpan.FromHours(9):selected.TimeOfDay);time.SetBounds(282,375,142,30);time.Font=Theme.Font(10.5f);Controls.Add(time);
            duration.SetBounds(440,375,152,30);duration.Font=Theme.Font(10.5f);Controls.Add(duration);
            endTime=LabelAt("",9,Theme.Muted,28,410,564,25);day.ValueChanged+=(s,e)=>UpdateEnd();time.ValueChanged+=(s,e)=>UpdateEnd();duration.ValueChanged+=(s,e)=>UpdateEnd();
            var hint=LabelAt("Đưa công việc có sẵn vào lịch, giữ nguyên liên kết Deadline.\nKhông tạo bản trùng và không cộng thưởng khi đặt lịch.",9,Theme.Muted,28,447,564,44);hint.BackColor=Theme.Sidebar;hint.Padding=new Padding(8,0,6,0);
            CancelAction=Theme.Button("Hủy");CancelAction.DialogResult=DialogResult.Cancel;CancelAction.SetBounds(328,500,110,42);Controls.Add(CancelAction);
            SaveButton=Theme.Button("Đưa vào lịch",true);SaveButton.SetBounds(450,500,142,42);Controls.Add(SaveButton);SaveButton.Click+=(s,e)=>Prepare();
            deadlines.SelectedIndexChanged+=(s,e)=>DeadlineChanged();tasks.SelectedIndexChanged+=(s,e)=>TaskChanged();DeadlineChanged();
        }
        Label LabelAt(string text,float size,Color color,int x,int y,int w,int h){var l=Theme.Label(text,size,color);l.SetBounds(x,y,w,h);Controls.Add(l);return l;}
        void Section(string number,string title,int top){var badge=new PillLabel {Text=number,BackColor=Theme.Sidebar,ForeColor=Theme.Purple,Font=Theme.Font(11,true),TextAlign=ContentAlignment.MiddleCenter};badge.SetBounds(28,top,30,30);Controls.Add(badge);var l=LabelAt(title,12,Theme.Ink,70,top,490,30);l.Font=Theme.Font(12,true);}
        void SetupCombo(ComboBox box,int top){box.DropDownStyle=ComboBoxStyle.DropDownList;box.IntegralHeight=false;box.MaxDropDownItems=9;box.DropDownWidth=700;box.Font=Theme.Font(10.5f);box.SetBounds(28,top,564,30);Controls.Add(box);}
        void DeadlineChanged(){
            var d=store.Data.Deadlines.FirstOrDefault(x=>x.Id==SelectedDeadlineId);
            due.Text=d==null?(deadlines.Items.Count==1?"Chưa có deadline đang làm. Tạo deadline ở trang Deadline trước.":"Chọn deadline có sẵn để xem các công việc bên trong."):"Hạn: "+d.Due.ToString("dd/MM/yyyy")+(String.IsNullOrEmpty(d.DueTime)?" · cuối ngày":" · "+d.DueTime);
            RefreshTasks(null);
        }
        void SetFilter(bool value){onlyPending=value;pending.BackColor=value?Theme.Purple:Color.White;pending.ForeColor=value?Color.White:Theme.Ink;all.BackColor=value?Color.White:Theme.Purple;all.ForeColor=value?Theme.Ink:Color.White;RefreshTasks(SelectedTaskId);}
        void RefreshTasks(string keep){
            binding=true;tasks.BeginUpdate();try{
                tasks.Items.Clear();tasks.Items.Add(new Choice {Caption=String.IsNullOrEmpty(SelectedDeadlineId)?"Chọn deadline trước…":"Chọn công việc…"});
                foreach(var t in store.DeadlineSchedulingTasks(SelectedDeadlineId,onlyPending))tasks.Items.Add(new Choice {Id=t.Id,Caption=t.Title+" · "+(store.IsScheduled(t.Id)?"Đã có lịch "+DateTime.ParseExact(t.Day,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("dd/MM")+" "+t.Time:"Chưa xếp lịch")+" · "+t.Duration+"p"});
                tasks.SelectedIndex=0;for(int i=1;i<tasks.Items.Count;i++)if(((Choice)tasks.Items[i]).Id==keep)tasks.SelectedIndex=i;
            }finally{tasks.EndUpdate();binding=false;}
            tasks.Enabled=tasks.Items.Count>1;pending.Enabled=all.Enabled=!String.IsNullOrEmpty(SelectedDeadlineId);
            empty.Text=!String.IsNullOrEmpty(SelectedDeadlineId) && tasks.Items.Count==1?(onlyPending?"Không có việc chưa xếp lịch. Có thể chọn Tất cả việc chưa xong.":"Deadline này không còn công việc chưa hoàn thành."):"";TaskChanged();
        }
        void TaskChanged(){
            if(binding)return;var task=store.AllTasks.FirstOrDefault(t=>t.Id==SelectedTaskId && !t.Done);
            day.Enabled=time.Enabled=duration.Enabled=SaveButton.Enabled=task!=null;
            if(task==null){summary.Text="Chọn deadline trước, sau đó chọn công việc.\nCác việc đã hoàn thành không xuất hiện trong danh sách.";endTime.Text="";return;}
            duration.Value=task.Duration;
            summary.Text=task.Title+"\n"+task.Category+" · Phần thưởng: "+task.Reward+" phút\n"+(store.IsScheduled(task.Id)?"Lịch hiện tại: "+DateTime.ParseExact(task.Day,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("dd/MM/yyyy")+" "+task.Time+" · lưu sẽ chuyển lịch này.":"Chưa xếp lịch · tên, nhóm, màu và phần thưởng được giữ nguyên.");UpdateEnd();
        }
        void UpdateEnd(){if(endTime==null || !duration.Enabled)return;var end=day.Value.Date.Add(TimeSpan.ParseExact(time.Value.ToString("HH:mm"),"hh\\:mm",CultureInfo.InvariantCulture)).AddMinutes((int)duration.Value);endTime.Text="Kết thúc dự kiến: "+end.ToString("HH:mm")+(end.Date>day.Value.Date?" · ngày "+end.ToString("dd/MM/yyyy"):"");}
        void Prepare(){
            Result=null;RescheduleConfirmed=false;
            var parent=store.Data.Deadlines.FirstOrDefault(d=>d.Id==SelectedDeadlineId && !d.Completed);var task=store.AllTasks.FirstOrDefault(t=>t.Id==SelectedTaskId && !t.Done);
            if(parent==null || task==null || !parent.TaskIds.Contains(task.Id)){MessageBox.Show(this,"Chọn deadline và một công việc chưa hoàn thành.","Chưa chọn công việc",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            string newDay=day.Value.ToString("yyyy-MM-dd"),newTime=time.Value.ToString("HH:mm");bool scheduled=store.IsScheduled(task.Id),changed=scheduled && (task.Day!=newDay || task.Time!=newTime || task.Duration!=(int)duration.Value);
            var end=DateTime.ParseExact(newDay+" "+newTime,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture).AddMinutes((int)duration.Value);bool late=end>parent.Due;
            if(changed || late){string message=changed?"Công việc đã có lịch "+task.Day+" "+task.Time+".\nChuyển sang "+day.Value.ToString("dd/MM/yyyy")+" "+newTime+"?":"Lịch này kết thúc sau hạn deadline. Vẫn lưu?";if(changed && late)message+="\nLưu ý: lịch mới kết thúc sau hạn deadline.";bool accepted=confirmationForTest!=null?confirmationForTest():MessageBox.Show(this,message,changed?"Xác nhận đổi lịch":"Lịch sau hạn",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes;if(!accepted)return;}
            RescheduleConfirmed=scheduled;Result=new TaskItem {Id=task.Id,Title=task.Title,Day=newDay,Time=newTime,Duration=(int)duration.Value,Reward=task.Reward,Category=task.Category,ColorHex=task.ColorHex};if(Prepared!=null)Prepared();
        }
        public void SelectDeadlineForTest(string id){for(int i=0;i<deadlines.Items.Count;i++)if(((Choice)deadlines.Items[i]).Id==id){deadlines.SelectedIndex=i;return;}throw new InvalidOperationException("Deadline not offered");}
        public void SelectTaskForTest(string id){for(int i=0;i<tasks.Items.Count;i++)if(((Choice)tasks.Items[i]).Id==id){tasks.SelectedIndex=i;return;}throw new InvalidOperationException("Task not offered");}
        public bool OffersTaskForTest(string id){return tasks.Items.Cast<Choice>().Any(c=>c.Id==id);}
        public void ShowAllForTest(){all.PerformClick();}
        public void ConfirmForTest(Func<bool> reply){confirmationForTest=reply;}
        public bool OffersDeadlineForTest(string id){return deadlines.Items.Cast<Choice>().Any(c=>c.Id==id);}
        public bool TaskChoiceEnabledForTest {get{return tasks.Enabled;}}
        public bool ReadyForTest {get{return SaveButton.Enabled;}}
        public void SetScheduleForTest(DateTime value,int minutes){day.Value=value.Date;time.Value=DateTime.Today.Add(value.TimeOfDay);duration.Value=minutes;}
    }
}
