using System;
using System.Collections.Generic;
using System.Linq;

namespace Nhip {
    public class DailyFocusRow {
        public string Id,Title,Category;
        public DateTime Start,End;
        public double Seconds;
        public bool Draft,Running;
    }
    public class DailyActivity {
        public string Title,Category;
        public DateTime Start,End;
        public bool Leisure,Approximate;
        public double Seconds;
    }
    public class DailyWalletRow {
        public string Kind,Detail;
        public DateTime Start,End;
        public double Seconds;
        public bool Approximate;
    }
    public class DailyReport {
        public DateTime Day;
        public StatsBucket Summary;
        public List<DailyFocusRow> Focus=new List<DailyFocusRow>();
        public List<DailyActivity> Activities=new List<DailyActivity>();
        public List<DailyWalletRow> Wallet=new List<DailyWalletRow>();
        public List<TaskItem> Tasks=new List<TaskItem>();
    }
    public partial class Store {
        public DailyReport ReportForDay(DateTime day) {
            day=day.Date;DateTime now=Clock();
            var end=day.AddDays(1);long beginUtc=day.ToUniversalTime().Ticks,endUtc=end.ToUniversalTime().Ticks;
            var report=new DailyReport {Day=day,Summary=Statistics(day,end,"Ngày").Single()};
            string key=day.ToString("yyyy-MM-dd");report.Tasks=Data.Tasks.Where(t=>t.Day==key).OrderBy(t=>t.Time).ThenBy(t=>t.Title).ToList();
            foreach(var session in Data.FocusSessions)AddDayFocus(report,session.Id,session.Title,session.Category,session.Slices,beginUtc,endUtc,false,false);
            var active=Data.ActiveFocus;
            if(active!=null) {
                var slices=active.Slices.Select(s=>new TimeSlice {Start=s.Start,End=s.End}).ToList();
                if(active.Running && now.Ticks>active.Anchor)slices.Add(new TimeSlice {Start=active.Anchor,End=now.Ticks});
                AddDayFocus(report,active.Id,active.Title,active.Category,slices,beginUtc,endUtc,true,active.Running);
            }
            foreach(var entry in Data.History) {
                DateTime at;if(!DateTime.TryParse(entry.At,null,System.Globalization.DateTimeStyles.RoundtripKind,out at))continue;
                at=at.ToLocalTime();
                if(entry.Detail=="Giải trí" && entry.Seconds<0)AddDayLeisure(report,at.AddSeconds(entry.Seconds),at,day,end,true);
                else if(at>=day && at<end && entry.Seconds!=0)report.Wallet.Add(new DailyWalletRow {Start=at,End=at,Kind=entry.Seconds>0?"Thưởng":"Thu hồi",Detail=entry.Detail,Seconds=entry.Seconds});
            }
            double remaining=Data.Running?Math.Max(0,Data.Balance-Math.Max(0,(now.Ticks-Data.AnchorTicks)/(double)TimeSpan.TicksPerSecond)):Data.Balance;
            if(Data.Running && remaining<Data.Balance) {
                DateTime start=Utc(Data.AnchorTicks).ToLocalTime();
                AddDayLeisure(report,start,start.AddSeconds(Data.Balance-remaining),day,end,false);
            }
            report.Focus=report.Focus.OrderByDescending(s=>s.Start).ToList();
            report.Activities=report.Activities.OrderBy(s=>s.Start).ToList();
            report.Wallet=report.Wallet.OrderByDescending(s=>s.Start).ToList();
            report.Summary.StudySeconds=report.Focus.Where(r=>r.Category=="Học tập").Sum(r=>r.Seconds);
            report.Summary.WorkSeconds=report.Focus.Where(r=>r.Category!="Học tập").Sum(r=>r.Seconds);
            report.Summary.EntertainmentMinutes=report.Activities.Where(r=>r.Leisure).Sum(r=>r.Seconds)/60;
            report.Summary.EarnedMinutes=report.Wallet.Where(r=>r.Kind=="Thưởng").Sum(r=>r.Seconds)/60;
            report.Summary.RevokedMinutes=-report.Wallet.Where(r=>r.Kind=="Thu hồi").Sum(r=>r.Seconds)/60;
            return report;
        }
        static void AddDayFocus(DailyReport report,string id,string title,string category,IEnumerable<TimeSlice> slices,long from,long to,bool draft,bool running) {
            var clipped=slices.Select(s=>new TimeSlice {Start=Math.Max(s.Start,from),End=Math.Min(s.End,to)}).Where(s=>s.End>s.Start).ToList();
            if(clipped.Count==0)return;
            report.Focus.Add(new DailyFocusRow {Id=id,Title=title,Category=category,Start=Utc(clipped.Min(s=>s.Start)).ToLocalTime(),End=Utc(clipped.Max(s=>s.End)).ToLocalTime(),Seconds=SliceSeconds(clipped),Draft=draft,Running=running});
            foreach(var s in clipped)report.Activities.Add(new DailyActivity {Title=title,Category=category,Start=Utc(s.Start).ToLocalTime(),End=Utc(s.End).ToLocalTime(),Seconds=(s.End-s.Start)/(double)TimeSpan.TicksPerSecond});
        }
        static void AddDayLeisure(DailyReport report,DateTime start,DateTime end,DateTime from,DateTime to,bool approximate) {
            start=Max(start,from);end=Min(end,to);if(end<=start)return;
            double seconds=(end-start).TotalSeconds;
            report.Activities.Add(new DailyActivity {Title=approximate?"Giải trí · theo mốc lưu":"Giải trí đang chạy",Start=start,End=end,Seconds=seconds,Leisure=true,Approximate=approximate});
            report.Wallet.Add(new DailyWalletRow {Kind="Giải trí",Detail=approximate?"Phân bổ theo mốc lưu":"Đang sử dụng",Start=start,End=end,Seconds=-seconds,Approximate=approximate});
        }
    }
}
