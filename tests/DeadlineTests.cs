using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Nhip;

static class DeadlineTests {
    public static void Run(Action<bool,string> check,string root) {
        string dir=Path.Combine(root,"deadlines");var s=new Store(dir);
        var d=new Deadline{Id="d1",Title="Nộp đồ án",DueDay="2026-11-30",Notes="Bản cuối"};s.SaveDeadline(d);
        check(s.Data.Deadlines.Count==1 && s.Data.Tasks.Count==0,"deadline creates without changing calendar");
        d=s.Data.Deadlines[0];check(d.Due.Date==new DateTime(2026,11,30) && d.Due.Hour==23 && d.Due.Minute==59,"date-only deadline ends on selected day");
        var a=new TaskItem{Id="a",Title="Code",Day="2026-10-24",Time="14:00",Duration=120,Reward=20,Category="Học tập"};s.AddDeadlineTask(d.Id,a);
        check(s.Data.Tasks.Count==0 && s.Data.UnscheduledTasks.Count==1 && s.DeadlineTasks(d).Count()==1,"pending task excluded from calendar");
        bool rejected=false;try{s.CompleteDeadline(d.Id,true);}catch(InvalidOperationException){rejected=true;}check(rejected,"unfinished deadline cannot complete");
        d=s.Data.Deadlines[0];s.SetDone("a",true);s.SetDone("a",true);check(s.Data.Balance==1200,"pending completion awards only once");
        s.CompleteDeadline(d.Id,true);string stamp=s.Data.Deadlines[0].CompletedAt;s.CompleteDeadline(d.Id,true);check(s.Data.Balance==1200 && stamp==s.Data.Deadlines[0].CompletedAt,"parent completion grants no bonus and keeps original timestamp");
        s.SetDone("a",false);check(!s.Data.Deadlines[0].Completed && s.Data.Balance==0,"undo child reopens deadline and reverses exact award");
        s.ScheduleDeadlineTask(new TaskItem{Id="a",Title="Code",Day="2026-12-01",Time="14:00",Duration=120,Reward=20,Category="Học tập"});
        check(s.Data.Tasks.Count==1 && s.Data.UnscheduledTasks.Count==0 && s.Data.Deadlines[0].DueDay=="2026-11-30","scheduling moves existing child without moving due date");
        s.SetDone("a",true);s.Reschedule("a","2026-10-25","15:00",90);check(s.Data.Tasks[0].Done && s.Data.Balance==1200 && s.ParentDeadline("a").Id=="d1","calendar move preserves link completion and credits");
        s.SaveDeadline(new Deadline{Id="d2",Title="Deadline 2",DueDay="2026-12-20",DueTime="18:00"});rejected=false;try{s.AttachTask("d2","a");}catch(InvalidOperationException){rejected=true;}check(rejected,"one task cannot belong to two deadlines");
        s.DetachTask("d1","a");check(s.Data.Tasks.Count==1 && s.Data.Balance==1200 && s.ParentDeadline("a")==null,"detach keeps task and reward");
        s.AttachTask("d2","a");s.CompleteDeadline("d2",true);s.DeleteDeadline("d2");check(s.Data.Tasks.Count==1 && s.Data.Balance==1200 && s.ParentDeadline("a")==null,"delete parent keeps scheduled work and credit");
        s.AddDeadlineTask("d1",new TaskItem{Id="b",Title="Kiểm thử",Day="2026-10-23",Time="09:00",Duration=60,Reward=15,Category="Học tập"});s.DeleteDeadline("d1");check(s.AllTasks.Any(t=>t.Id=="b") && s.Data.UnscheduledTasks.Count==1,"delete parent preserves pending work for relinking");
        var reloaded=new Store(dir);check(reloaded.AllTasks.Count()==2 && reloaded.Data.Balance==1200,"new fields and preserved tasks survive reload");
        s.SaveDeadline(new Deadline{Id="d3",Title="Cuối cùng",DueDay="2026-10-24"});s.AttachTask("d3","b");s.SetDone("b",true);s.CompleteDeadline("d3",true);s.Delete("b");check(!s.Data.Deadlines[0].Completed && s.Data.Deadlines[0].TaskIds.Count==0 && s.Data.Balance==1200,"deleting completed child refunds and reopens parent");
        rejected=false;try{s.CompleteDeadline("d3",true);}catch(InvalidOperationException){rejected=true;}check(rejected,"empty parent cannot be completed");
        var status=new Deadline{DueDay="2026-10-24",DueTime="12:00"};check(status.Status(new DateTime(2026,10,20))=="Đang làm" && status.Status(new DateTime(2026,10,23))=="Sắp đến hạn" && status.Status(new DateTime(2026,10,25))=="Quá hạn","deadline states respect exact due time");
        string legacy=Path.Combine(root,"deadline-legacy");Directory.CreateDirectory(legacy);string json="{\"Version\":1,\"Tasks\":[],\"History\":[],\"Balance\":732,\"Running\":false,\"Goals\":[],\"FocusSessions\":[]}";File.WriteAllText(Path.Combine(legacy,"nhip.json"),json);var old=new Store(legacy);check(old.Data.Deadlines.Count==0 && old.Data.UnscheduledTasks.Count==0 && old.Data.Balance==732 && File.ReadAllText(old.FilePath)==json,"opening legacy data initializes deadline collections without rewriting original");
        old.SaveDeadline(new Deadline{Id="new",Title="Deadline đầu tiên",DueDay="2026-12-01"});check(new Store(legacy).Data.Balance==732,"legacy balance survives first deadline save");
        var persisted=new Store(legacy);check(persisted.Data.Deadlines.Single().Title=="Deadline đầu tiên" && persisted.Data.Deadlines[0].Due.Day==1,"active deadline dates and title survive reload");
        var child=new TaskItem{Id="persist-child",Title="Tiếng Việt có dấu",Day="2026-11-01",Time="08:00",Duration=60,Reward=12,Category="Học tập"};persisted.AddDeadlineTask("new",child);persisted.SetDone(child.Id,true);persisted.CompleteDeadline("new",true);
        var completed=new Store(legacy);check(completed.Data.Deadlines.Single().Completed && completed.ParentDeadline(child.Id).Id=="new" && completed.AllTasks.Single().Done,"completed parent and pending child links survive reload");
        completed.CompleteDeadline("new",false);completed.SavePendingTask(new TaskItem{Id=child.Id,Title="Tên đã sửa",Day=child.Day,Time=child.Time,Duration=90,Reward=30,Category=child.Category});check(completed.AllTasks.Single().Awarded==12 && completed.Data.Balance==1452,"editing done pending task preserves original award");
        completed.ScheduleDeadlineTask(new TaskItem{Id=child.Id,Title="Tên đã sửa",Day=child.Day,Time=child.Time,Duration=90,Reward=30,Category=child.Category});check(completed.Data.Tasks.Single().Done && completed.Data.Tasks.Single().Awarded==12 && completed.Data.Balance==1452,"scheduling completed pending task cannot award twice");
        completed.SetDone(child.Id,false);check(completed.Data.Balance==732,"undo after pending edit and scheduling reverses original award");
        var exact=new Deadline{DueDay="2026-12-01",DueTime="18:25"};check(exact.Due==new DateTime(2026,12,1,18,25,0),"explicit deadline time uses exact selected minute");
        rejected=false;try{completed.SaveDeadline(new Deadline{Id="bad",Title="Sai ngày",DueDay="2026-02-30"});}catch(InvalidDataException){rejected=true;}check(rejected && completed.Data.Deadlines.Count==1,"invalid due date cannot alter saved deadlines");
        string goodTitle=completed.Data.Deadlines[0].Title;Directory.CreateDirectory(completed.FilePath+".tmp");rejected=false;try{completed.SaveDeadline(new Deadline{Id="new",Title="Không được lưu",DueDay="2026-12-02"});}catch(IOException){rejected=true;}catch(UnauthorizedAccessException){rejected=true;}check(rejected && completed.Data.Deadlines[0].Title==goodTitle,"deadline save failure rolls back in-memory edits");Directory.Delete(completed.FilePath+".tmp");
        var edit=new Store(Path.Combine(root,"task-deadline-edit"));
        edit.SaveDeadline(new Deadline{Id="one",Title="Đồ án 1",DueDay="2026-12-01"});edit.SaveDeadline(new Deadline{Id="two",Title="Đồ án 2",DueDay="2026-12-02"});
        var block=new TaskItem{Id="block",Title="Block lịch",Day="2026-11-01",Time="09:00",Duration=90,Reward=20,Category="Học tập",ColorHex="#123456"};edit.SaveTaskWithDeadline(block,"one");
        check(edit.Data.Tasks.Count==1 && edit.ParentDeadline("block").Id=="one" && edit.Data.Balance==0,"new calendar block attaches directly without reward");
        edit.SetDone("block",true);var draft=Store.CopyDraft(edit.Data.Tasks[0]);draft.Id="block";draft.Title="Đổi tên";draft.Reward=30;
        edit.SaveTaskWithDeadline(draft,"two");check(edit.Data.Deadlines.First(x=>x.Id=="one").TaskIds.Count==0 && edit.ParentDeadline("block").Id=="two" && edit.Data.Tasks.Count==1,"edit switches deadline without duplicating calendar block");
        check(edit.Data.Tasks[0].Done && edit.Data.Tasks[0].Awarded==20 && edit.Data.Balance==1200 && edit.Data.Tasks[0].Day=="2026-11-01" && edit.Data.Tasks[0].ColorHex=="#123456","changing deadline preserves schedule color completion and original reward");
        edit.SaveTaskWithDeadline(draft,"two");check(edit.Data.Deadlines.First(x=>x.Id=="two").TaskIds.Count==1,"repeated save does not duplicate deadline link");
        edit.SaveTaskWithDeadline(draft,null);check(edit.ParentDeadline("block")==null && edit.Data.Tasks.Count==1 && edit.Data.Balance==1200,"edit can detach deadline while keeping completed block and credits");
        edit.SaveTaskWithDeadline(draft,"one");var originalTitle=edit.Data.Tasks[0].Title;var invalid=Store.CopyDraft(draft);invalid.Id="block";invalid.Title="Unsaved";rejected=false;try{edit.SaveTaskWithDeadline(invalid,"missing");}catch(InvalidOperationException){rejected=true;}check(rejected && edit.Data.Tasks[0].Title==originalTitle && edit.ParentDeadline("block").Id=="one","missing deadline rejects entire task edit");
        edit.CompleteDeadline("one",true);edit.SaveTaskWithDeadline(draft,"one");check(edit.Data.Deadlines.First(x=>x.Id=="one").Completed && edit.Data.Balance==1200,"editing existing block in completed deadline keeps handover and credits");
        var another=Store.CopyDraft(draft);another.Title="Không được thêm";rejected=false;try{edit.SaveTaskWithDeadline(another,"one");}catch(InvalidOperationException){rejected=true;}check(rejected && edit.Data.Tasks.Count==1,"edit cannot add new block to completed deadline");
        edit.SaveTaskWithDeadline(draft,"two");check(!edit.Data.Deadlines.First(x=>x.Id=="one").Completed && edit.ParentDeadline("block").Id=="two","moving last block out reopens empty completed deadline");
        Directory.CreateDirectory(edit.FilePath+".tmp");rejected=false;try{edit.SaveTaskWithDeadline(invalid,"one");}catch(IOException){rejected=true;}catch(UnauthorizedAccessException){rejected=true;}check(rejected && edit.ParentDeadline("block").Id=="two" && edit.Data.Tasks[0].Title==originalTitle && edit.Data.Balance==1200,"failed combined save rolls back block link and reward together");Directory.Delete(edit.FilePath+".tmp");
        var editReload=new Store(Path.Combine(root,"task-deadline-edit"));check(editReload.ParentDeadline("block").Id=="two" && editReload.Data.Tasks[0].Done && editReload.Data.Balance==1200,"combined task and deadline edit survives reopening");
    }
}
