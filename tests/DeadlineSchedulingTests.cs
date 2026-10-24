using System;
using System.IO;
using System.Linq;
using Nhip;

static class DeadlineSchedulingTests {
    public static void Run(Action<bool,string> check,string root){
        var s=new Store(Path.Combine(root,"deadline-scheduling"));s.SaveDeadline(new Deadline{Id="a",Title="Đồ án",DueDay="2026-12-01"});s.SaveDeadline(new Deadline{Id="b",Title="Báo cáo",DueDay="2026-12-02"});
        s.AddDeadlineTask("a",new TaskItem{Id="pending",Title="Kiểm thử",Day="2026-10-24",Time="09:00",Duration=90,Reward=30,Category="Học tập",ColorHex="#704DE8"});
        s.AddDeadlineTask("b",new TaskItem{Id="other",Title="Việc deadline khác",Day="2026-10-24",Time="09:00",Duration=60,Reward=15,Category="Công việc"});
        s.Upsert(new TaskItem{Id="scheduled",Title="Việc đã có lịch",Day="2026-10-24",Time="10:00",Duration=60,Reward=20,Category="Công việc"});s.AttachTask("a","scheduled");
        check(s.DeadlineSchedulingTasks("a",true).Select(t=>t.Id).SequenceEqual(new[]{"pending"}),"deadline chooser defaults to only unscheduled unfinished children of selected parent");
        check(s.DeadlineSchedulingTasks("a",false).Count()==2 && !s.DeadlineSchedulingTasks("a",false).Any(t=>t.Id=="other"),"all unfinished filter includes scheduled work but excludes other deadline children");
        string original=File.ReadAllText(s.FilePath);s.DeadlineSchedulingTasks("a",true).ToList();check(File.ReadAllText(s.FilePath)==original,"reading scheduling choices cannot write data");
        int total=s.AllTasks.Count();s.ScheduleTaskFromDeadline("a","pending","2026-10-25","15:15",90,false);
        check(s.AllTasks.Count()==total && s.IsScheduled("pending") && !s.Data.UnscheduledTasks.Any(t=>t.Id=="pending"),"scheduling pending child moves same task into calendar without creating duplicate");
        var placed=s.Data.Tasks.First(t=>t.Id=="pending");check(placed.Title=="Kiểm thử" && placed.Category=="Học tập" && placed.Reward==30 && placed.ColorHex=="#704DE8" && !placed.Done && placed.Awarded==0 && s.Data.Balance==0,"deadline scheduling preserves task details without completion or reward");
        check(s.ParentDeadline("pending").Id=="a" && s.Data.Deadlines.First(d=>d.Id=="a").TaskIds.Count==2 && s.Data.Deadlines[0].DueDay=="2026-12-01","placing child retains parent relationship and deadline due date");
        bool rejected=false;try{s.ScheduleTaskFromDeadline("a","scheduled","2026-10-26","14:00",120,false);}catch(InvalidOperationException){rejected=true;}
        check(rejected && s.Data.Tasks.First(t=>t.Id=="scheduled").Day=="2026-10-24","scheduled task cannot move without explicit confirmation");
        s.ScheduleTaskFromDeadline("a","scheduled","2026-10-26","14:00",120,true);check(s.Data.Tasks.Count(t=>t.Id=="scheduled")==1 && s.Data.Tasks.First(t=>t.Id=="scheduled").Duration==120 && s.AllTasks.Count()==total,"confirmed move changes existing schedule exactly once");
        rejected=false;try{s.ScheduleTaskFromDeadline("b","pending","2026-10-27","09:00",60,true);}catch(InvalidOperationException){rejected=true;}check(rejected && s.ParentDeadline("pending").Id=="a","stale wrong-parent selection is rejected without moving link");
        rejected=false;try{s.ScheduleTaskFromDeadline("a","missing","2026-10-27","09:00",60,false);}catch(InvalidOperationException){rejected=true;}check(rejected && s.AllTasks.Count()==total,"missing task cannot create a phantom calendar entry");
        s.SetDone("pending",true);rejected=false;try{s.ScheduleTaskFromDeadline("a","pending","2026-10-27","09:00",60,true);}catch(InvalidOperationException){rejected=true;}check(rejected && s.Data.Balance==1800 && !s.DeadlineSchedulingTasks("a",false).Any(t=>t.Id=="pending"),"newly completed child is excluded and rejected at commit without extra reward");
        s.SetDone("scheduled",true);s.CompleteDeadline("a",true);check(!s.DeadlineSchedulingTasks("a",false).Any(),"completed deadline exposes no scheduling candidates");
        rejected=false;try{s.ScheduleTaskFromDeadline("a","scheduled","2026-10-28","09:00",60,true);}catch(InvalidOperationException){rejected=true;}check(rejected && s.Data.Deadlines.First(d=>d.Id=="a").Completed,"completed deadline cannot be changed through source scheduling");
        s.CompleteDeadline("a",false);s.SetDone("scheduled",false);var before=s.Data.Tasks.First(t=>t.Id=="scheduled").Time;Directory.CreateDirectory(s.FilePath+".tmp");rejected=false;try{s.ScheduleTaskFromDeadline("a","scheduled","2026-10-29","08:00",60,true);}catch(IOException){rejected=true;}catch(UnauthorizedAccessException){rejected=true;}Directory.Delete(s.FilePath+".tmp");check(rejected && s.Data.Tasks.First(t=>t.Id=="scheduled").Time==before && s.ParentDeadline("scheduled").Id=="a","failed schedule save rolls back task schedule and retains link");
        var reloaded=new Store(Path.Combine(root,"deadline-scheduling"));check(reloaded.ParentDeadline("scheduled").Id=="a" && reloaded.Data.Tasks.Count(t=>t.Id=="pending")==1 && reloaded.Data.Tasks.First(t=>t.Id=="scheduled").Day=="2026-10-26","calendar placement and original task IDs survive reopen");
    }
}
