using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Linq;
using System.Threading;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;

using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI.Hooks;


namespace TimeBasedRanks
{
    [ApiVersion(1, 16)]
    public class Tbr : TerrariaPlugin
    {
        private IDbConnection _db;
        public static Database dbManager;
        public static TbrConfig config = new TbrConfig();
        private static TbrTimers _timers;

        internal static readonly TbrPlayers Players = new TbrPlayers();

        public override string Author
        {
            get { return "White"; }
        }

        public override string Description
        {
            get { return "TShock group movements for users based on time played"; }
        }

        public override string Name
        {
            get { return "Time Based Ranks"; }
        }

        public override Version Version
        {
            get { return new Version(0, 1); }
        }


        public Tbr(Main game)
            : base(game)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);

                var t = new Thread(delegate()
                {
                    dbManager.SaveAllPlayers();
                    Log.ConsoleInfo("Saved players successfully");
                });
                t.Start();
                t.Join();
            }
            base.Dispose(disposing);
        }

        public override void Initialize()
        {
            switch (TShock.Config.StorageType.ToLower())
            {
                case "sqlite":
                    _db = new SqliteConnection(string.Format("uri=file://{0},Version=3",
                        Path.Combine(TShock.SavePath, "TBRData.sqlite")));
                    break;
                case "mysql":
                    try
                    {
                        var host = TShock.Config.MySqlHost.Split(':');
                        _db = new MySqlConnection
                        {
                            ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4}",
                                host[0],
                                host.Length == 1 ? "3306" : host[1],
                                TShock.Config.MySqlDbName,
                                TShock.Config.MySqlUsername,
                                TShock.Config.MySqlPassword
                                )
                        };
                    }
                    catch (MySqlException x)
                    {
                        Log.Error(x.ToString());
                        throw new Exception("MySQL not setup correctly.");
                    }
                    break;
                default:
                    throw new Exception("Invalid storage type.");
            }

            dbManager = new Database(_db);

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            PlayerHooks.PlayerPostLogin += PostLogin;
        }

        /// <summary>
        /// Handles greet events. 
        /// </summary>
        /// <param name="args"></param>
        private static void OnGreet(GreetPlayerEventArgs args)
        {
            var ply = TShock.Players[args.Who];

            if (ply == null)
                return;

            if (ply.IsLoggedIn)
                PostLogin(new PlayerPostLoginEventArgs(ply));

        }

        private static void OnLeave(LeaveEventArgs args)
        {
            if (TShock.Players[args.Who] == null)
                return;

            var ply = TShock.Players[args.Who];

            if (!ply.IsLoggedIn) return;

            var player = Players.GetByUsername(ply.UserAccountName);
            if (player == null)
                return;

            dbManager.SavePlayer(player);
            player.tsPlayer = null;
        }

        /// <summary>
        /// Handles login events. Syncs the player's stored stats if they have them
        /// </summary>
        /// <param name="e"></param>
        private static void PostLogin(PlayerPostLoginEventArgs e)
        {
            if (e.Player == null)
                return;

            var player = Players.GetByUsername(e.Player.UserAccountName);

            if (player != null)
                player.tsPlayer = e.Player;

            else
            {
                player = new TbrPlayer(e.Player.UserAccountName, 0, DateTime.UtcNow.ToString("G"),
                    DateTime.UtcNow.ToString("G"), 0) {tsPlayer = e.Player};
                Players.Add(player);

                if (!dbManager.InsertPlayer(player))
                    Log.ConsoleError("[TimeRanks] Failed to create storage for {0}.", player.name);
                else
                    Log.ConsoleInfo("[TimeRanks] Created storage for {0}.", player.name);
            }


            if (!config.AutoStartUsers || e.Player.Group.Name != config.StartGroup || config.Groups.Count < 1)
                return;

            TShock.Users.SetUserGroup(
                TShock.Users.GetUserByName(e.Player.UserAccountName),
                config.Groups.Keys.ToList()[0]);

            if (TShock.Config.DisableUUIDLogin)
                player.RankUp();
        }

        private void OnInitialize(EventArgs e)
        {
            var configPath = Path.Combine(TShock.SavePath, "TimeRanks.json");
            (config = TbrConfig.Read(configPath)).Write(configPath);

            _timers = new TbrTimers();

            _timers.Start();

            if (config.Groups.Keys.Count > 0)
                if (String.Equals(config.StartGroup, config.Groups.Keys.ToList()[0],
                    StringComparison.CurrentCultureIgnoreCase))
                {
                    Log.ConsoleError("[Time Based Ranks] Initialization cancelled due to configuration error: " +
                                     "StartGroup is the same as first rank name");

                    ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                    return;
                }

            if (config.CreateNonExistantGroups)
                CreateGroups();

            Commands.ChatCommands.Add(new Command("tbr.rank.check", Check, "check", "checktime", "ct")
            {
                HelpText = "Displays text about your current and upcoming ranks, as well as time infomration"
            });

            Commands.ChatCommands.Add(new Command("tbr.start", StartRank, "start", "startrank", "sr")
            {
                HelpText = "Switches a user into the starting group for the rank system"
            });

            dbManager.InitialSyncPlayers();
        }

        private static void StartRank(CommandArgs args)
        {
            if (!args.Player.IsLoggedIn)
                args.Player.SendErrorMessage("You must login to use this");
            else
            {
                TShock.Users.SetUserGroup(
                    TShock.Users.GetUserByName(args.Player.UserAccountName), config.Groups.Keys.ElementAt(0));

                Players.GetByUsername(args.Player.UserAccountName).RankUp();

                args.Player.SendSuccessMessage("Success! You will now gain ranks over time");
            }
        }

        private static void Check(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                var str = string.Join(" ", args.Parameters);
                var players = Players.GetListByUsername(str).ToList();
                var tsplayers = TShock.Utils.FindPlayer(str);

                if (tsplayers.Count > 1)
                    TShock.Utils.SendMultipleMatchError(args.Player, tsplayers.Select(p => p.Name));

                if (players.Count > 1)
                    TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.name));

                else
                    switch (players.Count)
                    {
                        case 0:
                            args.Player.SendErrorMessage("No player matched your query '{0}'", str);
                            break;
                        case 1:
                            if (players[0] == null)
                            {
                                args.Player.SendErrorMessage("---");
                                return;
                            }

                            args.Player.SendSuccessMessage("{0}'s registration date: " + players[0].firstLogin,
                                players[0].name);
                            args.Player.SendSuccessMessage(
                                "{0}'s total registered time: " + players[0].TotalRegisteredTime, players[0].name);
                            args.Player.SendSuccessMessage("{0}'s total time played: " + players[0].TimePlayed,
                                players[0].name);

                            if (players[0].Online)
                            {
                                args.Player.SendSuccessMessage("{0}'s current rank position: " +
                                                               players[0].GroupPosition + " (" + players[0].Group + ")",
                                    players[0].name);
                                args.Player.SendSuccessMessage("{0}'s next rank: " + players[0].NextGroupName,
                                    players[0].name);
                                args.Player.SendSuccessMessage("{0}'s next rank in: " + players[0].NextRankTime,
                                    players[0].name);
                            }
                            else
                                args.Player.SendSuccessMessage("{0} was last online: " + players[0].lastLogin +
                                                               " (" + players[0].LastOnline.ElapsedString() + " ago)",
                                    players[0].name);
                            break;
                    }
            }
            else
            {
                if (args.Player == TSPlayer.Server)
                {
                    args.Player.SendErrorMessage("Sorry, the server doesn't have stats to check (yet?)");
                    return;
                }
                var player = Players.GetByUsername(args.Player.UserAccountName);
                args.Player.SendSuccessMessage("Your registration date: " + player.firstLogin);
                args.Player.SendSuccessMessage("Your total registered time: " + player.TotalRegisteredTime);
                args.Player.SendSuccessMessage("Your total time played: " + player.TimePlayed);
                args.Player.SendSuccessMessage("Your current rank position: "
                                               + player.GroupPosition + " (" + player.Group + ")");
                args.Player.SendSuccessMessage("Your next rank: " + player.NextGroupName);
                args.Player.SendSuccessMessage("Next rank in: " + player.NextRankTime);
            }
        }

        /// <summary>
        /// Creates TShock groups from groups defined in the configuration file, if they do not exist
        /// </summary>
        private static void CreateGroups()
        {
            var addedGroups = new List<string>();
            if (config.Groups.Count > 0)
                foreach (var group in
                    config.Groups.Keys.Where(group => !TShock.Groups.GroupExists(group)))
                {
                    TShock.Groups.AddGroup(group, string.Empty);
                    addedGroups.Add(group);
                }

            if (addedGroups.Count > 0)
                Log.ConsoleInfo("[TimeRanks]: Auto-created {0} groups as defined in config: {1}",
                    addedGroups.Count, string.Join(", ", addedGroups));
        }
    }
}
