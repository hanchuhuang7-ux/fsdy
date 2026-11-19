using System;

namespace Frontline.Core
{
    public readonly struct ManorEventChoice
    {
        public readonly string title;
        public readonly int coin, food, timber, arms, workers, approval;

        internal ManorEventChoice(string title,int coin = 0,int food = 0,int timber = 0,int arms = 0,int workers = 0,int approval = 0)
        {
            this.title=title; this.coin=coin; this.food=food; this.timber=timber;
            this.arms=arms; this.workers=workers; this.approval=approval;
        }
    }

    public sealed class ManorEvent
    {
        public readonly string title, detail;
        public readonly ManorEventChoice first, second;

        internal ManorEvent(string title,string detail,ManorEventChoice first,ManorEventChoice second)
        {
            this.title=title; this.detail=detail; this.first=first; this.second=second;
        }
    }

    public sealed partial class Manor
    {
        // The date alone identifies a pending dispatch. Zero also represents an already settled dispatch.
        public int eventDay;
        public ManorEvent ActiveEvent
        {
            get
            {
                if (eventDay == 0) return null;
                int season = SeasonNumber(eventDay);
                return SeasonalEvents[2 * (season % 4) + (season / 4) % 2];
            }
        }

        // Each season alternates between its two reports in successive years.
        static readonly ManorEvent[] SeasonalEvents = {
            new ManorEvent("流民投奔","一户流民来到桥头，希望在春播前安顿下来。",
                new ManorEventChoice("安置一户",food:-4,workers:1),new ManorEventChoice("赠粮送行",food:-1,approval:5)),
            new ManorEvent("行商试市","行商带来一批修缮用的建材，愿意用低价试开市集。",
                new ManorEventChoice("购入建材",coin:-9,timber:4),new ManorEventChoice("收取过路税",coin:4)),
            new ManorEvent("工匠改良","工匠试制了几件新军械，愿用一笔工钱换领地接手。",
                new ManorEventChoice("购下军械",coin:-18,arms:3),new ManorEventChoice("留存金币")),
            new ManorEvent("边疆斥候急报","斥候带回边境消息，也找回了几件可用军械。",
                new ManorEventChoice("犒赏斥候",coin:-12,arms:2,approval:4),new ManorEventChoice("保持戒备")),
            new ManorEvent("丰收庆典","秋粮已入库。领民提议拿出几份粮食办一场庆典。",
                new ManorEventChoice("举办庆典",food:-6,approval:10),new ManorEventChoice("全部入库")),
            new ManorEvent("商队收粮","路过的商队出高价收购秋粮，领地也可留下这批粮食备冬。",
                new ManorEventChoice("售出秋粮",coin:13,food:-5),new ManorEventChoice("留粮备冬")),
            new ManorEvent("寒潮来袭","桥头结霜，几户领民请求多拨建材加固农舍。",
                new ManorEventChoice("加固农舍",timber:-4,approval:6),new ManorEventChoice("保留建材",approval:-4)),
            new ManorEvent("军备紧急征购","邻地商队急售一批军械，领地可趁此补足军备。",
                new ManorEventChoice("低价补充军械",coin:-24,arms:4),new ManorEventChoice("留金过冬"))
        };

        static int SeasonNumber(int atDay) => (atDay - 1) / SeasonDays;
        static bool ValidEventDay(int issuedDay,int currentDay) => issuedDay == 0 ||
            issuedDay > 0 && issuedDay <= currentDay && issuedDay % SeasonDays == 6 &&
            SeasonNumber(issuedDay) == SeasonNumber(currentDay);

        void UpdateSeasonEvents()
        {
            if (eventDay != 0 && (day - 1) % SeasonDays == 0)
            {
                var choice = ActiveEvent.second;
                // A delayed gift does not spend the last ration. If the default cannot be applied, archive the report.
                if (ChoiceBlock(choice) == null && (choice.food >= 0 || food > -choice.food)) ApplyChoice(choice);
                eventDay = 0;
            }
            if (eventDay == 0 && day % SeasonDays == 6) eventDay = day;
        }

        public string EventChoiceBlock(int issuedDay,int option)
        {
            if (eventDay == 0 || eventDay != issuedDay) return "这条急报已归档";
            if (option != 0 && option != 1) return "未知急报选项";
            return ChoiceBlock(option == 0 ? ActiveEvent.first : ActiveEvent.second);
        }

        string ChoiceBlock(ManorEventChoice choice)
        {
            if (coin + choice.coin < 0) return "金币不足";
            if (food + choice.food < 0) return "粮食不足";
            if (timber + choice.timber < 0) return "建材不足";
            if (arms + choice.arms < 0) return "军械不足";
            if (workers + choice.workers > Housing) return "没有空房";
            int cap = MaxValue * 100;
            if (Precise(coin + choice.coin,remainder.coin) > cap || Precise(food + choice.food,remainder.food) > cap ||
                Precise(timber + choice.timber,remainder.timber) > cap || Precise(arms + choice.arms,remainder.arms) > cap)
                return "物资将超出仓储上限";
            return null;
        }

        void ApplyChoice(ManorEventChoice choice)
        {
            coin += choice.coin;
            food += choice.food;
            timber += choice.timber;
            arms += choice.arms;
            workers += choice.workers;
            approval = ClampApproval(approval + choice.approval);
        }

        public void ResolveEvent(int issuedDay,int option)
        {
            string reason = EventChoiceBlock(issuedDay,option);
            if (reason != null) throw new InvalidOperationException(reason);
            ApplyChoice(option == 0 ? ActiveEvent.first : ActiveEvent.second);
            eventDay = 0;
        }
    }
}
