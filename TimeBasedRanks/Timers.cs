using System;
using System.Linq;
using System.Timers;
using TShockAPI;

namespace TimeBasedRanks
{
    public class TbrTimers
    {
        private Timer aTimer;
        private Timer uTimer;
        private Timer bTimer;
        private Timer cTimer;

        public TbrTimers()
        {
            bTimer = new Timer(TBR.config.SavePlayerStatsInterval * 1000);
            uTimer = new Timer(TBR.config.IncrementTimeInterval * 1000);
            aTimer = new Timer(TBR.config.CheckAfkStatusInterval * 1000);
            cTimer = new Timer(TBR.config.CheckOldAccountsInterval * 1000);
        }

        public void Start()
        {
            aTimer.Enabled = true;
            aTimer.Elapsed += afkTimer;

            uTimer.Enabled = true;
            uTimer.Elapsed += updateTimer;

            bTimer.Enabled = true;
            bTimer.Elapsed += backupTimer;

            cTimer.Enabled = true;
            cTimer.Elapsed += accountCheckTimer;
        }

        private void afkTimer(object sender, ElapsedEventArgs args)
        {
            /* TODO
             * Add complex afk timer so IcyPhoenix's dumbass player's can't
             * bypass it in some unheard of weird way
             */
        }

        private void updateTimer(object sender, ElapsedEventArgs args)
        {
            foreach (var player in TBR.Tools.Players.Where(player => player.online))
            {
                player.time += TBR.config.IncrementTimeInterval;

                if (player.index == -1) 
                    continue;

                player.points += (int)(TBR.config.IncrementTimeInterval / TBR.config.PointDivisor);

                if (player.points < TBR.config.Groups[player.group].rankCost) 
                    continue;

                if (player.getNextGroupName == player.group)
                    continue;

                player.points = 0;
                
                TShock.Users.SetUserGroup(
                    TShock.Users.GetUserByName(player.name), player.getNextGroupName);

                foreach (var cmd in player.rankInfo.commands)
                {
                    var command = cmd.StartsWith("/") ? cmd : "/" + cmd;
                    Commands.HandleCommand(TBR.config.UseConfigToExecuteRankUpCommands
                        ? TSServerPlayer.Server
                        : TShock.Players[player.index],
                        command);
                }

                TShock.Players[player.index].SendWarningMessage("You have ranked up!");
                TShock.Players[player.index].SendWarningMessage("Your current rank position: "
                                                                + player.getGroupPosition + " (" + player.@group + ")");
                TShock.Players[player.index].SendWarningMessage("Your next rank: " + player.getNextGroupName);
                TShock.Players[player.index].SendWarningMessage("Next rank in: " + player.getNextRankTime);
            }
        }

        private void backupTimer(object sender, ElapsedEventArgs args)
        {
            TBR.dbManager.saveAllPlayers();
        }

        private void accountCheckTimer(object sender, ElapsedEventArgs args)
        {
            foreach (var player in TBR.Tools.Players.Where(player => !player.online))
            {
                var group = TShock.Groups.GetGroupByName(player.group);
                if (group.HasPermission("tbr.ignorederank"))
                    continue;

                var ts = (TimeSpan) player.getLastOnline[0];

                if (ts.TotalSeconds < player.getRankInfo.derankCost)
                    continue;

                var groupIndex = TBR.config.Groups.Keys.ToList().IndexOf(player.group) - 1;

                if (groupIndex < 0)
                    continue;

                var user = TShock.Users.GetUserByName(player.name);

                TShock.Users.SetUserGroup(user, TBR.config.Groups.Keys.ToList()[groupIndex]);
                Log.ConsoleInfo(user.Name + " has been dropped a rank due to inactivity");
            }
        }
    }
}
