using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TShockAPI;

namespace TimeBasedRanks
{
    public class TbrPlayers
    {
        private readonly List<TbrPlayer> _players = new List<TbrPlayer>();

        /// <summary>
        /// Creates a new TbrPlayer object to add to the _players list
        /// </summary>
        /// <param name="name"></param>
        /// <param name="time"></param>
        /// <param name="firstLogin"></param>
        /// <param name="lastLogin"></param>
        /// <param name="points"></param>
        public void Add(string name, int time, string firstLogin, string lastLogin, int points)
        {
            _players.Add(new TbrPlayer(name, time, firstLogin, lastLogin, points));
        }

        /// <summary>
        /// Adds a TbrPlayer object to the _players list
        /// </summary>
        /// <param name="player"></param>
        public void Add(TbrPlayer player)
        {
            _players.Add(player);
        }

        /// <summary>
        /// Returns the first player that has a username matching the string, or null
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public TbrPlayer GetByUsername(string username)
        {
            return _players.FirstOrDefault(p => p.name == username);
        }

        /// <summary>
        /// Returns a list of players that have usernames matching the given string
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public IEnumerable<TbrPlayer> GetListByUsername(string username)
        {
            return _players.Where(p => p.name.ToLowerInvariant().Contains(username.ToLowerInvariant()));
        }

        public IEnumerable<TbrPlayer> Players { get { return _players; } }

        /// <summary>
        /// Enumerable of offline players
        /// </summary>
        public IEnumerable<TbrPlayer> Offline { get { return _players.Where(p => !p.Online); } }
        /// <summary>
        /// Enumberable of online players
        /// </summary>
        public IEnumerable<TbrPlayer> Online { get { return _players.Where(p => p.Online); } } 
    }

    public class TbrPlayer
    {
        /// <summary>
        /// tsPlayer this player is linked with
        /// </summary>
        public TSPlayer tsPlayer;

        public bool Online { get { return tsPlayer != null; } }

        /// <summary>
        /// Equivalent to tsPlayer.Username
        /// </summary>
        public readonly string name;
        /// <summary>
        /// First time player logged in
        /// </summary>
        public readonly string firstLogin;
        /// <summary>
        /// Last time player was seen
        /// </summary>
        public string lastLogin;

        /// <summary>
        /// TShock group
        /// </summary>
        public string Group
        {
            get
            {
                return !Online
                    ? TShock.Users.GetUserByName(name).Group
                    : tsPlayer.Group.Name;
            }
        }

        /// <summary>
        /// Current rank info
        /// </summary>
        public RankInfo RankInfo
        {
            get
            {
                return ConfigContainsGroup
                    ? (Group == Tbr.config.StartGroup
                        ? new RankInfo(Tbr.config.Groups.Keys.ElementAt(0), 0, 0,
                            Tbr.config.Groups.Values.ElementAt(0).commands)
                        : Tbr.config.Groups.First(g => g.Key == Group).Value)
                    : new RankInfo("none", 0, 0, null);
            }
        }

        public int time;
        public int points;

        /// <summary>
        /// cctor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="time"></param>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="points"></param>
        public TbrPlayer(string name, int time, string first, string last, int points)
        {
            this.time = time;
            this.name = name;
            firstLogin = first;
            lastLogin = last;
            this.points = points;
        }

        /// <summary>
        /// Amount of time the player has been registered for. (days, hours, minutes, seconds)
        /// </summary>
        public string TotalRegisteredTime
        {
            get
            {
                DateTime reg;
                DateTime.TryParse(firstLogin, out reg);

                var ts = DateTime.UtcNow - reg;
                return ts.ElapsedString();
            }
        }

        /// <summary>
        /// Amount of time the player has played for. (days, hours, minutes, seconds)
        /// </summary>
        public string TimePlayed
        {
            get
            {
                var ts = new TimeSpan(0, 0, 0, time);
                return ts.ElapsedString();
            }
        }

        /// <summary>
        /// Returns a TimeSpan representing the time since the player was last online
        /// </summary>
        public TimeSpan LastOnline
        {
            get
            {
                DateTime last;
                DateTime.TryParse(lastLogin, out last);

                var ts = DateTime.UtcNow - last;

                return ts;
            }
        }

        /// <summary>
        /// Returns a string representation of the time required for the next rank. (days, hours, minutes, seconds)
        /// </summary>
        public string NextRankTime
        {
            get
            {
                if (!ConfigContainsGroup)
                    return "group is not a part of the ranking system";

                if (NextGroupName == "max rank achieved")
                    return "max rank achieved";

                var reqPoints = NextRankInfo.rankCost;
                var ts = new TimeSpan(0, 0, 0, reqPoints - points);

                return ts.ElapsedString();
            }
        }

        /// <summary>
        /// Returns the player's position inside the rank ladder
        /// </summary>
        public string GroupPosition
        {
            get
            {
                if (!ConfigContainsGroup)
                    return "group is not a part of the ranking system";

                return (Tbr.config.Groups.Keys.ToList().IndexOf(Group) + 1)
                       + " / " + Tbr.config.Groups.Keys.Count;
            }
        }

        /// <summary>
        /// Returns a string value representing the next group the player will move into
        /// </summary>
        public string NextGroupName
        {
            get
            {
                if (RankInfo.nextGroup == Group)
                    return "max rank achieved";

                if (!ConfigContainsGroup)
                    return "group is not part of the ranking system";

                return RankInfo.nextGroup;
            }
        }

        /// <summary>
        /// Next rank info
        /// </summary>
        private RankInfo NextRankInfo
        {
            get
            {
                return ConfigContainsGroup
                    ? (RankInfo.nextGroup == Group
                        ? new RankInfo("max rank", 0,
                            Tbr.config.Groups.Values.ElementAt(Tbr.config.Groups.Values.Count - 1).derankCost,
                            Tbr.config.Groups.Values.ElementAt(Tbr.config.Groups.Values.Count - 1).commands)
                        : Tbr.config.Groups[RankInfo.nextGroup])
                    : new RankInfo("none", 0, 0, null);
            }
        }


        private static readonly Regex CleanCommandRegex = new Regex(@"^\/?(\w*\w)");

        public void RankUp()
        {
            foreach (var s in RankInfo.commands)
            {
                var cmd = CleanCommandRegex.Match(s).Groups[1].Value;

                var command = Commands.ChatCommands.FirstOrDefault(c => c.HasAlias(cmd));
                if (command == null) continue;

                var text = s.Replace(TShock.Config.CommandSpecifier, String.Empty).Trim();
                text = text.Replace("{player}", tsPlayer.Name).Replace("{group}", tsPlayer.Group.Name);

                command.CommandDelegate.Invoke(new CommandArgs(text, tsPlayer, text.Split(' ').ToList()));
            }
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