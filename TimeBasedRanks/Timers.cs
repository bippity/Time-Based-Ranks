using System;
using System.Linq;
using System.Timers;
using TShockAPI;

namespace TimeBasedRanks
{
    public class TbrTimers
    {
        private readonly Timer _aTimer;
        private readonly Timer _uTimer;
        private readonly Timer _bTimer;
        private readonly Timer _cTimer;

        public TbrTimers()
        {
            _bTimer = new Timer(Tbr.config.SavePlayerStatsInterval * 1000);
            _uTimer = new Timer(Tbr.config.IncrementTimeInterval * 1000);
            _aTimer = new Timer(Tbr.config.CheckAfkStatusInterval * 1000);
            _cTimer = new Timer(Tbr.config.CheckOldAccountsInterval * 1000);
        }

        public void Start()
        {
            _aTimer.Enabled = true;
            _aTimer.Elapsed += AfkTimer;

            _uTimer.Enabled = true;
            _uTimer.Elapsed += UpdateTimer;

            _bTimer.Enabled = true;
            _bTimer.Elapsed += BackupTimer;

            _cTimer.Enabled = true;
            _cTimer.Elapsed += AccountCheckTimer;
        }

        private static void AfkTimer(object sender, ElapsedEventArgs args)
        {
            /* TODO
             * Add complex afk timer so IcyPhoenix's player's can't
             * bypass it in some unheard of weird way
             */
        }

        private static void UpdateTimer(object sender, ElapsedEventArgs args)
        {
            foreach (var player in Tbr.Players.Online)
            {
                player.time += Tbr.config.IncrementTimeInterval;

                if (player.tsPlayer == null) 
                    continue;

                player.points += (Tbr.config.IncrementTimeInterval / Tbr.config.PointDivisor);

                if (player.points < Tbr.config.Groups[player.Group].rankCost) 
                    continue;

                if (player.NextGroupName == player.Group)
                    continue;

                player.points = 0;
                
                TShock.Users.SetUserGroup(
                    TShock.Users.GetUserByName(player.name), player.NextGroupName);

                player.RankUp();

                player.tsPlayer.SendWarningMessage("You have ranked up!");
                player.tsPlayer.SendWarningMessage("Your current rank position: "
                                                                + player.GroupPosition + " (" + player.Group + ")");
                player.tsPlayer.SendWarningMessage("Your next rank: " + player.NextGroupName);
                player.tsPlayer.SendWarningMessage("Next rank in: " + player.NextRankTime);
            }
        }

        private static void BackupTimer(object sender, ElapsedEventArgs args)
        {
            Tbr.dbManager.SaveAllPlayers();
        }

        private static void AccountCheckTimer(object sender, ElapsedEventArgs args)
        {
            foreach (var player in Tbr.Players.Offline)
            {
                var group = TShock.Groups.GetGroupByName(player.Group);
                if (group.HasPermission("tbr.ignorederank"))
                    continue;

                var ts = player.LastOnline;

                if (ts.TotalSeconds < player.RankInfo.derankCost)
                    continue;

                var groupIndex = Tbr.config.Groups.Keys.ToList().IndexOf(player.Group) - 1;

                if (groupIndex < 0)
                    continue;

                var user = TShock.Users.GetUserByName(player.name);

                TShock.Users.SetUserGroup(user, Tbr.config.Groups.Keys.ElementAt(groupIndex));
                Log.ConsoleInfo(user.Name + " has been dropped a rank due to inactivity");
            }
        }
    }
}
