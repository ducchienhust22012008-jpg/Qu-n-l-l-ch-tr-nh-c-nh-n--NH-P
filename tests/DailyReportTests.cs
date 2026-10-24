using System;
using System.IO;
using System.Linq;
using Nhip;

static class DailyReportTests {
    public static void Run(Action<bool,string> check,string root){
        var day=new DateTime(2026,10,23);var store=new Store(Path.Combine(root,"daily-report"));var now=day.AddHours(23).AddMinutes(30).ToUniversalTime();store.Clock=()=>now;
        store.Upsert(new TaskItem{Id="daily-task",Title="Việc trong ngày",Day=day.ToString("yyyy-MM-dd"),Time="09:00",Duration=60,Reward=20,Category="Học tập"});store.SetDone("daily-task",true);
        store.StartFocus("Phiên qua đêm","Học tập");now=now.AddMinutes(15);store.PauseFocus();now=now.AddMinutes(30);store.ResumeFocus();now=now.AddMinutes(45);store.FinishFocus();
        var before=File.ReadAllText(store.FilePath);var report=store.ReportForDay(day);var next=store.ReportForDay(day.AddDays(1));
        check(report.Focus.Count==1 && report.Focus[0].Seconds==900 && next.Focus.Single().Seconds==2700,"daily focus splits cross-midnight session and excludes paused gap");
        check(report.Focus[0].Start==day.AddHours(23.5) && report.Focus[0].End==day.AddHours(23.75) && next.Focus[0].Start==day.AddDays(1).AddMinutes(15),"daily focus row bounds follow clipped actual slices");
        check(report.Summary.FocusSeconds==900 && next.Summary.FocusSeconds==2700 && report.Activities.Sum(a=>a.Seconds)==900,"daily summary details and chart agree on focus duration");
        check(report.Tasks.Count==1 && report.Summary.Completed==1 && next.Tasks.Count==0 && next.Summary.Planned==0,"day tasks use scheduled date without leaking across days");
        check(report.Summary.EarnedMinutes==20 && report.Wallet.Single().Kind=="Thưởng" && next.Summary.EarnedMinutes==0,"daily reward is attributed to transaction date");
        check(before==File.ReadAllText(store.FilePath) && store.Data.Balance==1200,"reading daily report never writes data or changes balance");
        now=day.AddDays(1).AddHours(9).ToUniversalTime();store.StartFocus("Phiên đang chạy","Công việc");now=now.AddSeconds(90);var active=store.ReportForDay(day.AddDays(1));
        check(active.Focus.Count==2 && active.Focus.Single(r=>r.Draft).Running && active.Summary.WorkSeconds==90,"daily view includes unsaved active focus exactly once");
        check(active.Focus.Sum(r=>r.Seconds)==active.Summary.FocusSeconds && active.Activities.Where(a=>!a.Leisure).Sum(a=>a.Seconds)==active.Summary.FocusSeconds,"active daily totals match detail rows and chart");
        store.PauseFocus();now=now.AddHours(1);var paused=store.ReportForDay(day.AddDays(1));check(paused.Summary.WorkSeconds==90 && paused.Focus.Single(r=>r.Draft).Running==false,"paused focus adds no break time to daily report");
        store.FinishFocus();var saved=store.ReportForDay(day.AddDays(1));check(saved.Focus.Count==2 && saved.Focus.All(r=>!r.Draft) && saved.Summary.FocusSeconds==paused.Summary.FocusSeconds,"saving active focus does not double count daily duration");
        store.Data.History.Add(new Entry{At=day.AddDays(1).AddMinutes(20).ToUniversalTime().ToString("o"),Detail="Giải trí",Seconds=-2400});
        var previousDay=store.ReportForDay(day);var laterDay=store.ReportForDay(day.AddDays(1));
        check(previousDay.Summary.EntertainmentMinutes==20 && laterDay.Summary.EntertainmentMinutes==20,"recorded entertainment is split across local midnight");
        check(previousDay.Activities.Single(a=>a.Leisure).Start==day.AddHours(23).AddMinutes(40) && previousDay.Activities.Single(a=>a.Leisure).End==day.AddDays(1),"entertainment timeline is clipped to selected day boundaries");
        check(previousDay.Wallet.Where(w=>w.Kind=="Giải trí").Sum(w=>-w.Seconds)/60==previousDay.Summary.EntertainmentMinutes && previousDay.Activities.Single(a=>a.Leisure).Approximate,"legacy leisure details match aggregate and are marked approximate");
        now=day.AddDays(1).AddHours(12).ToUniversalTime();store.SetDone("daily-task",false);var refund=store.ReportForDay(day.AddDays(1));check(refund.Summary.RevokedMinutes==20 && refund.Wallet.Any(w=>w.Kind=="Thu hồi" && w.Seconds==-1200),"undo reward belongs to its actual transaction day");
        check(store.ReportForDay(day).Summary.Completed==0,"daily completed tasks explicitly reflect current task state");
        var wallet=new Store(Path.Combine(root,"daily-live-wallet"));now=day.AddHours(23).AddMinutes(50).ToUniversalTime();wallet.Clock=()=>now;wallet.Upsert(new TaskItem{Id="credit",Title="Nhận thưởng",Day=day.ToString("yyyy-MM-dd"),Time="09:00",Duration=60,Reward=30,Category="Công việc"});wallet.SetDone("credit",true);wallet.Start();now=now.AddMinutes(20);
        check(wallet.ReportForDay(day).Summary.EntertainmentMinutes==10 && wallet.ReportForDay(day.AddDays(1)).Summary.EntertainmentMinutes==10,"running entertainment is split across midnight without checkpoint writes");
        var live=wallet.ReportForDay(day.AddDays(1));check(live.Activities.Single(a=>a.Leisure).Approximate==false && live.Summary.EarnedMinutes==0 && live.Summary.RevokedMinutes==0,"live entertainment is not mistaken for reward or reversal");
        wallet.Pause();check(wallet.ReportForDay(day.AddDays(1)).Summary.EntertainmentMinutes==10,"settling leisure preserves daily total without double counting");
        var empty=wallet.ReportForDay(day.AddDays(20));check(empty.Focus.Count==0 && empty.Tasks.Count==0 && empty.Wallet.Count==0 && empty.Activities.Count==0 && empty.Summary.FocusSeconds==0,"empty day has no stale rows or activities");
        var reloaded=new Store(Path.Combine(root,"daily-report"));check(reloaded.ReportForDay(day).Summary.FocusSeconds==900,"daily report works after reopening existing persisted sessions");
    }
}
