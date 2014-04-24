using System;
using System.Diagnostics;
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
    [ApiVersion(1,15)]
    public class TBR : TerrariaPlugin
    {
        private IDbConnection DB;
        public static Tools Tools;
        public static Database dbManager;
        public static TRConfig config = new TRConfig();
        private static TbrTimers _timers;

        public override string Author { get { return "White"; } }
        public override string Description { get { return "TShock group movements for users based on time played"; } }
        public override string Name { get { return "Time Based Ranks"; } }
        public override Version Version { get { return new Version(0, 1); } }


        public TBR(Main game)
            : base(game) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);

                var t = new Thread(delegate()
                {
                    dbManager.saveAllPlayers();
                    Log.ConsoleInfo("Saved players successfully");
                });
                t.Start();
                t.Join();
            }
            base.Dispose(disposing);
        }

        public override void Initialize()
        {
            Tools = new Tools();

            switch (TShock.Config.StorageType.ToLower())
            {
                case "sqlite":
                    DB = new SqliteConnection(string.Format("uri=file://{0},Version=3",
                        Path.Combine(TShock.SavePath, "TBRData.sqlite")));
                    break;
                case "mysql":
                    try
                    {
                        var host = TShock.Config.MySqlHost.Split(':');
                        DB = new MySqlConnection
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

            dbManager = new Database(DB);

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            PlayerHooks.PlayerPostLogin += PostLogin;
        }

        /// <summary>
        /// Handles greet events. 
        /// </summary>
        /// <param name="args"></param>
        private void OnGreet(GreetPlayerEventArgs args)
        {
            if (TShock.Players[args.Who] == null)
                return;

            var ply = TShock.Players[args.Who];

            if (!TShock.Config.DisableUUIDLogin)
            {
                if (ply.IsLoggedIn)
                    PostLogin(new PlayerPostLoginEventArgs(ply));
                else
                {
                    var player = new TrPlayer("~^" + ply.Name, 0,
                        DateTime.UtcNow.ToString("G"), DateTime.UtcNow.ToString("G"), 0)
                        {index = args.Who, online = true};

                    Tools.Players.Add(player);
                }
            }
            else
            {
                var player = new TrPlayer("~^" + ply.Name, 0,
                    DateTime.UtcNow.ToString("G"), DateTime.UtcNow.ToString("G"), 0) 
                    {index = args.Who, online = true};

                Tools.Players.Add(player);
            }
        }

        private static void OnLeave(LeaveEventArgs args)
        {
            if (TShock.Players[args.Who] == null)
                return;

            var ply = TShock.Players[args.Who];

            if (ply.IsLoggedIn)
            {
                if (Tools.GetPlayerByName(ply.UserAccountName) == null)
                    return;

                var player = Tools.GetPlayerByName(ply.UserAccountName);
                dbManager.savePlayer(player);
                player.online = false;
                player.index = -1;
            }
            else
            {
                if (Tools.GetPlayerByName("~^" + ply.Name) == null)
                    return;

                var player = Tools.GetPlayerByName("~^" + ply.Name);
                player.online = false;
                player.index = -1;
            }
        }

        /// <summary>
        /// Handles login events. Syncs the player's stored stats if they have them
        /// </summary>
        /// <param name="e"></param>
        private void PostLogin(PlayerPostLoginEventArgs e)
        {
            if (e.Player == null)
                return;

            if (config.AutoStartUsers && e.Player.Group.Name == config.StartGroup &&
                config.Groups.Keys.Count > 0)
                TShock.Users.SetUserGroup(
                    TShock.Users.GetUserByName(e.Player.UserAccountName),
                    config.Groups.Keys.ToList()[0]);

            if (Tools.GetPlayerByName(e.Player.UserAccountName) != null)
            {
                var player = Tools.GetPlayerByName(e.Player.UserAccountName);

                player.index = e.Player.Index;
                player.online = true;
            }

            else
            {
                if (Tools.GetPlayerByName("~^" + e.Player.Name) != null)
                {
                    var player = Tools.GetPlayerByName("~^" + e.Player.Name);

                    player.name = e.Player.UserAccountName;
                    player.index = e.Player.Index;

                    player.online = true;

                    if (!dbManager.insertPlayer(player))
                        Log.ConsoleError("[TimeRanks] Failed to create storage for {0}.", player.name);
                    else
                        Log.ConsoleInfo("[TimeRanks] Created storage for {0}.", player.name);
                }
                else
                {
                    var player = new TrPlayer(e.Player.UserAccountName, 0, DateTime.UtcNow.ToString("G"),
                        DateTime.UtcNow.ToString("G"), 0) {index = e.Player.Index, online = true};

                    Tools.Players.Add(player);

                    if (!dbManager.insertPlayer(player))
                        Log.ConsoleError("[TimeRanks] Failed to create storage for {0}.", player.name);
                    else
                        Log.ConsoleInfo("[TimeRanks] Created storage for {0}.", player.name);
                }
            }
        }

        private void OnInitialize(EventArgs e)
        {
            var configPath = Path.Combine(TShock.SavePath, "TimeRanks.json");
            (config = TRConfig.Read(configPath)).Write(configPath);

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
                Tools.CreateGroups();

            Commands.ChatCommands.Add(new Command("tbr.rank.check", Check, "check", "checktime", "ct")
            {
                HelpText = "Displays text about your current and upcoming ranks, as well as time infomration"
            });

            Commands.ChatCommands.Add(new Command("tbr.start", StartRank, "start", "startrank", "sr")
            {
                HelpText = "Switches a user into the starting group for the rank system"
            });

            dbManager.Initialize();
        }

        private static void StartRank(CommandArgs args)
        {
            if (!args.Player.IsLoggedIn)
                args.Player.SendErrorMessage("You must login to use this");
            else
            {
                TShock.Users.SetUserGroup(
                    TShock.Users.GetUserByName(args.Player.UserAccountName),
                    config.Groups.Keys.ToList()[0]);
                args.Player.SendSuccessMessage("Success! You will now gain ranks over time");
            }
        }

        private static void Check(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                var str = string.Join(" ", args.Parameters);
                var player = Tools.GetPlayerListByName(str);

                if (player.Count > 1)
                    TShock.Utils.SendMultipleMatchError(args.Player, player.Select(p => p.name));

                else
                    switch (player.Count)
                    {
                        case 0:
                            args.Player.SendErrorMessage("No player matched your query '{0}'", str);
                            break;
                        case 1:
                            args.Player.SendSuccessMessage("{0}'s registration date: " + player[0].firstLogin,
                                player[0].name);
                            args.Player.SendSuccessMessage(
                                "{0}'s total registered time: " + player[0].getTotalRegisteredTime, player[0].name);
                            args.Player.SendSuccessMessage("{0}'s total time played: " + player[0].getTimePlayed,
                                player[0].name);

                            if (player[0].online)
                            {
                                args.Player.SendSuccessMessage("{0}'s current rank position: " +
                                                               player[0].getGroupPosition + " (" + player[0].group + ")",
                                    player[0].name);
                                args.Player.SendSuccessMessage("{0}'s next rank: " + player[0].getNextGroupName,
                                    player[0].name);
                                args.Player.SendSuccessMessage("{0}'s next rank in: " + player[0].getNextRankTime,
                                    player[0].name);
                            }
                            else
                                args.Player.SendSuccessMessage("{0} was last online: " + player[0].lastLogin +
                                                               " (" + player[0].getLastOnline[1] + " ago)",
                                                               player[0].name);
                            break;
                    }
            }
            else
            {
                if (args.Player == TSServerPlayer.Server)
                {
                    args.Player.SendErrorMessage("Sorry, the server doesn't have stats to check (yet?)!");
                    return;
                }
                var player = Tools.GetPlayerByName(args.Player.UserAccountName);
                args.Player.SendSuccessMessage("Your registration date: " + player.firstLogin);
                args.Player.SendSuccessMessage("Your total registered time: " + player.getTotalRegisteredTime);
                args.Player.SendSuccessMessage("Your total time played: " + player.getTimePlayed);
                args.Player.SendSuccessMessage("Your current rank position: " 
                    + player.getGroupPosition + " (" + player.group + ")");
                args.Player.SendSuccessMessage("Your next rank: " + player.getNextGroupName);
                args.Player.SendSuccessMessage("Next rank in: " + player.getNextRankTime);
            }
        }
    }
}
