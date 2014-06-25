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
             * Add complex afk timer so IcyPhoenix's dumbass player's can't
             * bypass it in some unheard of weird way
             */
        }

        private static void UpdateTimer(object sender, ElapsedEventArgs args)
        {
            foreach (var player in Tbr.tools.players.Where(player => player.online))
            {
                player.time += Tbr.config.IncrementTimeInterval;

                if (player.index == -1) 
                    continue;

                player.points += (int)(Tbr.config.IncrementTimeInterval / Tbr.config.PointDivisor);

                if (player.points < Tbr.config.Groups[player.Group].rankCost) 
                    continue;

                if (player.GetNextGroupName == player.Group)
                    continue;

                player.points = 0;
                
                TShock.Users.SetUserGroup(
                    TShock.Users.GetUserByName(player.name), player.GetNextGroupName);

                foreach (var cmd in player.RankInfo.commands)
                {
                    var command = cmd.StartsWith("/") ? cmd : "/" + cmd;
                    Commands.HandleCommand(Tbr.config.UseConfigToExecuteRankUpCommands
                        ? TSPlayer.Server
                        : TShock.Players[player.index],
                        command);
                }

                TShock.Players[player.index].SendWarningMessage("You have ranked up!");
                TShock.Players[player.index].SendWarningMessage("Your current rank position: "
                                                                + player.GetGroupPosition + " (" + player.Group + ")");
                TShock.Players[player.index].SendWarningMessage("Your next rank: " + player.GetNextGroupName);
                TShock.Players[player.index].SendWarningMessage("Next rank in: " + player.GetNextRankTime);
            }
        }

        private static void BackupTimer(object sender, ElapsedEventArgs args)
        {
            Tbr.dbManager.SaveAllPlayers();
        }

        private static void AccountCheckTimer(object sender, ElapsedEventArgs args)
        {
            foreach (var player in Tbr.tools.players.Where(player => !player.online))
            {
                var group = TShock.Groups.GetGroupByName(player.Group);
                if (group.HasPermission("tbr.ignorederank"))
                    continue;

                var ts = (TimeSpan) player.GetLastOnline[0];

                if (ts.TotalSeconds < player.GetRankInfo.derankCost)
                    continue;

                var groupIndex = Tbr.config.Groups.Keys.ToList().IndexOf(player.Group) - 1;

                if (groupIndex < 0)
                    continue;

                var user = TShock.Users.GetUserByName(player.name);

                TShock.Users.SetUserGroup(user, Tbr.config.Groups.Keys.ToList()[groupIndex]);
                Log.ConsoleInfo(user.Name + " has been dropped a rank due to inactivity");
            }
        }
    }
}
