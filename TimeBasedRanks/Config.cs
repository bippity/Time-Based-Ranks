using System;
using System.IO;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace TimeBasedRanks
{
    public class RankInfo
    {
        /// <summary>
        /// Amount of points required to achieve rank
        /// </summary>
        public readonly int rankCost;
        /// <summary>
        /// Group after this one
        /// </summary>
        public readonly string nextGroup;
        /// <summary>
        /// Lack of activity (time in seconds) required to lose this rank
        /// </summary>
        public readonly int derankCost;
        /// <summary>
        /// Commands to be executed upon achieving this rank
        /// </summary>
        public readonly string[] commands;

        public RankInfo(string nextGroup, int rankCost, int derankCost, string[] commands)
        {
            this.nextGroup = nextGroup;
            this.rankCost = rankCost;
            this.derankCost = derankCost;
            this.commands = commands;
        }
    }

    public class TbrConfig
    {
        /// <summary>
        /// How often to add 'points' (time) to an account
        /// </summary>
        public int IncrementTimeInterval = 1;
        /// <summary>
        /// 
        /// </summary>
        public int PointDivisor = 1;
        /// <summary>
        /// How often to check whether a player is afk
        /// </summary>
        public int CheckAfkStatusInterval = 1;
        /// <summary>
        /// How often to check offline accounts for activity
        /// </summary>
        public int CheckOldAccountsInterval = 3600;
        /// <summary>
        /// How often to save player stats
        /// </summary>
        public int SavePlayerStatsInterval = 600;
        /// <summary>
        /// Create config defined groups on startup if they don't exist in TShock
        /// </summary>
        public bool CreateNonExistantGroups = false;
        /// <summary>
        /// Group users will start in. Default is the default TShock group: "default"
        /// </summary>
        public string StartGroup = "default";
        /// <summary>
        /// Users are automatically put into the start group upon first registering
        /// </summary>
        public bool AutoStartUsers = true;
        /// <summary>
        /// Groups to level up or down into
        /// </summary>
        public readonly Dictionary<string, RankInfo> Groups = new Dictionary<string, RankInfo>();
        //    = new Dictionary<string, rankInfo>
        //{
        //    {"newbie", new rankInfo("worker", 100, 100, new[] {"help", "check"})},
        //    {"worker", new rankInfo("vip", 200, 100, new[] {"help", "check"})},
        //    {"vip", new rankInfo("vip", 300, 100, new[] {"help", "check"})}
        //};


        public void Write(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static TbrConfig Read(string path)
        {
            if (!File.Exists(path))
                return new TbrConfig();
            return JsonConvert.DeserializeObject<TbrConfig>(File.ReadAllText(path));
        }
    }
}
