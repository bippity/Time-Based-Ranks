using System.IO;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace TimeBasedRanks
{
    public class rankInfo
    {
        public readonly int rankCost;
        public readonly string nextGroup;
        public readonly int derankCost;
        public readonly string[] commands;

        public rankInfo(string nextGroup, int rankCost, int derankCost, string[] commands)
        {
            this.nextGroup = nextGroup;
            this.rankCost = rankCost;
            this.derankCost = derankCost;
            this.commands = commands;
        }
    }

    public class TRConfig
    {
        public int IncrementTimeInterval = 1;
        public float PointDivisor = 1;
        public int CheckAfkStatusInterval = 1;
        public int CheckOldAccountsInterval = 3600;
        public int SavePlayerStatsInterval = 600;
        public bool CreateNonExistantGroups = false;
        public string StartGroup = "default";
        public bool AutoStartUsers = true;
        public bool UseConfigToExecuteRankUpCommands = false;

        public readonly Dictionary<string, rankInfo> Groups = new Dictionary<string, rankInfo>
        {
            {"newbie", new rankInfo("worker", 100, 100, new[] {"help", "check"})},
            {"worker", new rankInfo("vip", 200, 100, new[] {"help", "check"})},
            {"vip", new rankInfo("vip", 300, 100, new[] {"help", "check"})}
        };


        public void Write(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static TRConfig Read(string path)
        {
            if (!File.Exists(path))
                return new TRConfig();
            return JsonConvert.DeserializeObject<TRConfig>(File.ReadAllText(path));
        }
    }
}
