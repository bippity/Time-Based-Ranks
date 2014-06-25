using System;
using System.Linq;
using System.Text;
using TShockAPI;

namespace TimeBasedRanks
{
    public class TrPlayer
    {
        public int index;
        public bool online;
        public string name;
        public readonly string firstLogin;
        public string lastLogin;

        public string Group
        {
            get
            {
                return !online
                    ? TShock.Users.GetUserByName(name).Group
                    : TShock.Players[index].Group.Name;
            }
        }

        public rankInfo RankInfo
        {
            get { return GetRankInfo; }
        }

        public int time;
        public int points;

        public TrPlayer(string name, int time, string first, string last, int points)
        {
            this.time = time;
            this.name = name;
            firstLogin = first;
            lastLogin = last;
            this.points = points;
        }

        public string GetTotalRegisteredTime
        {
            get
            {
                var sb = new StringBuilder();

                DateTime reg;
                DateTime.TryParse(firstLogin, out reg);

                var ts = DateTime.UtcNow - reg;

                var months = ts.Days/30;

                if (months > 0)
                    sb.Append(string.Format("{0} month{1}~", months, AppendString(months)));
                if (ts.Days > 0)
                    sb.Append(string.Format("{0} day{1}~", ts.Days, AppendString(ts.Days)));
                if (ts.Hours > 0)
                    sb.Append(string.Format("{0} hour{1}~", ts.Hours, AppendString(ts.Hours)));
                if (ts.Minutes > 0)
                    sb.Append(string.Format("{0} minute{1}~", ts.Minutes, AppendString(ts.Minutes)));
                if (ts.Seconds > 0)
                    sb.Append(string.Format("{0} second{1}", ts.Seconds, AppendString(ts.Seconds)));


                return string.Join(", ", sb.ToString().Split('~'));
            }
        }

        public string GetTimePlayed
        {
            get
            {
                var sb = new StringBuilder();

                var ts = new TimeSpan(0, 0, 0, time);

                var months = ts.Days/30;

                if (months > 0)
                    sb.Append(string.Format("{0} month{1}~", months, AppendString(months)));
                if (ts.Days > 0)
                    sb.Append(string.Format("{0} day{1}~", ts.Days, AppendString(ts.Days)));
                if (ts.Hours > 0)
                    sb.Append(string.Format("{0} hour{1}~", ts.Hours, AppendString(ts.Hours)));
                if (ts.Minutes > 0)
                    sb.Append(string.Format("{0} minute{1}~", ts.Minutes, AppendString(ts.Minutes)));
                if (ts.Seconds > 0)
                    sb.Append(string.Format("{0} second{1}", ts.Seconds, AppendString(ts.Seconds)));


                return string.Join(", ", sb.ToString().Split('~'));
            }
        }

        public object[] GetLastOnline
        {
            get
            {
                var sb = new StringBuilder();
                DateTime last;
                DateTime.TryParse(lastLogin, out last);

                var ts = DateTime.UtcNow - last;

                var months = ts.Days/30;

                if (months > 0)
                    sb.Append(string.Format("{0} month{1}~", months, AppendString(months)));
                if (ts.Days > 0)
                    sb.Append(string.Format("{0} day{1}~", ts.Days, AppendString(ts.Days)));
                if (ts.Hours > 0)
                    sb.Append(string.Format("{0} hour{1}~", ts.Hours, AppendString(ts.Hours)));
                if (ts.Minutes > 0)
                    sb.Append(string.Format("{0} minute{1}~", ts.Minutes, AppendString(ts.Minutes)));
                if (ts.Seconds > 0)
                    sb.Append(string.Format("{0} second{1}", ts.Seconds, AppendString(ts.Seconds)));

                return new object[] {ts, string.Join(", ", sb.ToString().Split('~'))};
            }
        }

        public string GetNextRankTime
        {
            get
            {
                if (!ConfigContainsGroup)
                    return "group is not a part of the ranking system";

                if (GetNextGroupName == "max rank achieved")
                    return "max rank achieved";


                var reqPoints = GetNextRankInfo.rankCost;
                var ts = new TimeSpan(0, 0, 0, reqPoints - points);

                var sb = new StringBuilder();

                var months = ts.Days/30;

                if (months > 0)
                    sb.Append(string.Format("{0} month{1}~", months, AppendString(months)));
                if (ts.Days > 0)
                    sb.Append(string.Format("{0} day{1}~", ts.Days, AppendString(ts.Days)));
                if (ts.Hours > 0)
                    sb.Append(string.Format("{0} hour{1}~", ts.Hours, AppendString(ts.Hours)));
                if (ts.Minutes > 0)
                    sb.Append(string.Format("{0} minute{1}~", ts.Minutes, AppendString(ts.Minutes)));
                if (ts.Seconds > 0)
                    sb.Append(string.Format("{0} second{1}", ts.Seconds, AppendString(ts.Seconds)));

                return string.Join(", ", sb.ToString().Split('~'));
            }
        }

        public string GetGroupPosition
        {
            get
            {
                if (!ConfigContainsGroup)
                    return "0 / 0";

                return (Tbr.config.Groups.Keys.ToList().IndexOf(Group) + 1)
                       + " / " + Tbr.config.Groups.Keys.Count;
            }
        }

        /// <summary>
        /// Returns a string value of the next group the player will move into
        /// </summary>
        public string GetNextGroupName
        {
            get
            {
                if (RankInfo.nextGroup == Group)
                    return "max rank achieved";
                return !ConfigContainsGroup
                    ? "group is not part of the ranking system"
                    : GetRankInfo.nextGroup;
            }
        }

        private rankInfo GetNextRankInfo
        {
            get
            {
                return ConfigContainsGroup
                    ? (RankInfo.nextGroup == Group
                        ? new rankInfo("max rank", 0,
                            Tbr.config.Groups.Values.ToList()[Tbr.config.Groups.Values.ToList().Count - 1].derankCost,
                            Tbr.config.Groups.Values.ToList()[Tbr.config.Groups.Values.ToList().Count - 1].commands)
                        : Tbr.config.Groups.First(g => g.Key == RankInfo.nextGroup).Value)
                    : new rankInfo("none", 0, 0, null);
            }
        }

        public rankInfo GetRankInfo
        {
            get
            {
                return ConfigContainsGroup
                    ? (Group == Tbr.config.StartGroup
                        ? new rankInfo(Tbr.config.Groups.Keys.ToList()[0], 0, 0, 
                            Tbr.config.Groups.Values.ToList()[0].commands)
                        : Tbr.config.Groups.First(g => g.Key == Group).Value)
                    : new rankInfo("none", 0, 0, null);
            }
        }

        /// <summary>
        /// Appends a suffix to a string if the number is > 1 or 0
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        private static string AppendString(int number)
        {
            return number > 1 || number == 0 ? "s" : "";
        }

        /// <summary>
        /// Returns a boolean value indicating whether the config file contains the player's group
        /// </summary>
        private bool ConfigContainsGroup
        {
            get { return Tbr.config.Groups.Keys.Contains(Group); }
        }
    }
}
