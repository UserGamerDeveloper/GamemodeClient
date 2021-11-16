using CitizenFX.Core;
using CitizenFX.Core.Native;
using CitizenFX.Core.UI;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;

namespace gamemode
{
    public class Create : BaseScript
    {
        public Create()
        {
            Tick += BotsDisabled;
            EventHandlers["gameStart"] += new Action(OnGameStart);
            EventHandlers["playerSpawned"] += new Action(OnCreateCommand);
        }

        private async Task BotsDisabled()
        {
            //API.SetVehicleDensityMultiplierThisFrame(0f);
            //API.SetPedDensityMultiplierThisFrame(0f);
            //API.SetRandomVehicleDensityMultiplierThisFrame(0f);
            //API.SetParkedVehicleDensityMultiplierThisFrame(0f);
            //API.SetScenarioPedDensityMultiplierThisFrame(0f, 0f);
        }

        private async void OnGameStart()
        {
        }

        private async void OnCreateCommand()
        {
            RegisterCommand("coord", new Action<int, List<object>, string>((source, args, raw) =>
            {
                try
                {
                    Debug.WriteLine("[Checkpoint] " + args[0].ToString()+ " " + GetEntityCoords(GetPlayerPed(-1), true).ToString() +" "+ GetEntityHeading(GetPlayerPed(-1)));
                    TriggerEvent("chat:addMessage", new
                    {
                        color = new[] { 255, 0, 0 },
                        args = new[] { "[Checkpoint] create" }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{ex}");
                }
            }), false);
            RegisterCommand("start", new Action<int, List<object>, string>((source, args, raw) =>
            {
                try
                {
                    SpawnManager.SpawnPlayer();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{ex}");
                }
            }), false);
        }
    }

    public class HardCap : BaseScript
    {
        public HardCap()
        {
            BaseScript.Delay(1000);
            Debug.WriteLine("HARDCAP Resource constructor called");
            Tick += PlayerActivatedCheck;
        }

        public void RegisterEventHandler(string trigger, Delegate callback)
        {
            EventHandlers[trigger] += callback;
        }

        internal async Task PlayerActivatedCheck()
        {
            if (NetworkIsSessionStarted())
            {
                TriggerServerEvent("HardCap.PlayerActivated");
                Tick -= PlayerActivatedCheck;
            }
            await Task.FromResult(0);
        }
    }

    public class SpawnManager : BaseScript
    {
        // This won't matter for our purposes anyway since we handle /re/spawn ourselves
        static Vector3 BlockerInitialSpawnCoords = new Vector3(825.0297f, 1272.458f, 360.3454f);
        static Vector3 InitialSpawnCoords = new Vector3(661.333f, 1366.123f, 325.9667f);
        static Model defaultModel = PedHash.Trevor;
        static float heading = 0.0f;

        public SpawnManager()
        {
            Tick += SpawnCheck;
        }

        public void RegisterEventHandler(string trigger, Delegate callback)
        {
            EventHandlers[trigger] += callback;
        }

        internal async Task SpawnCheck()
        {
            bool playerPedExists = (Game.PlayerPed.Handle != 0);
            bool playerActive = NetworkIsPlayerActive(PlayerId());

            if (playerPedExists && playerActive/*Game.PlayerPed.IsDead IsEntityDead(GetPlayerPed(-1))*/)
            {
            }
            Tick -= SpawnCheck;
            await SpawnPlayer();
            Screen.Fading.FadeIn(0);
            ShutdownLoadingScreen();
        }

        private static bool _spawnLock = false;

        public static void FreezePlayer(int playerId, bool freeze)
        {
            var ped = GetPlayerPed(playerId);

            SetPlayerControl(playerId, !freeze, 0);

            if (!freeze)
            {
                if (!IsEntityVisible(ped))
                    SetEntityVisible(ped, true, false);

                if (!IsPedInAnyVehicle(ped, true))
                    SetEntityCollision(ped, true, true);

                FreezeEntityPosition(ped, false);
                //SetCharNeverTargetted(ped, false)
                SetPlayerInvincible(playerId, false);
            }
            else
            {
                if (IsEntityVisible(ped))
                    SetEntityVisible(ped, false, false);

                SetEntityCollision(ped, false, true);
                FreezeEntityPosition(ped, true);
                //SetCharNeverTargetted(ped, true)
                SetPlayerInvincible(playerId, true);

                if (IsPedFatallyInjured(ped))
                    ClearPedTasksImmediately(ped);
            }
        }

        public static async Task SpawnPlayer( )
        {
            Model skin = defaultModel;
            float x = InitialSpawnCoords.X;
            float y = InitialSpawnCoords.Y;
            float z = InitialSpawnCoords.Z;
            float heading = SpawnManager.heading;

            if (_spawnLock)
                return;

            _spawnLock = true;

            DoScreenFadeOut(500);

            while (IsScreenFadingOut())
            {
                await Delay(1);
            }

            FreezePlayer(PlayerId(), true);
            await Game.Player.ChangeModel(skin);
            SetPedDefaultComponentVariation(GetPlayerPed(-1));
            RequestCollisionAtCoord(x, y, z);

            var ped = GetPlayerPed(-1);

            SetEntityCoordsNoOffset(ped, x, y, z, false, false, false);
            NetworkResurrectLocalPlayer(x, y, z, heading, true, true);
            ClearPedTasksImmediately(ped);
            RemoveAllPedWeapons(ped, false);
            ClearPlayerWantedLevel(PlayerId());

            while (!HasCollisionLoadedAroundEntity(ped))
            {
                await Delay(1);
            }

            ShutdownLoadingScreen();
            DoScreenFadeIn(500);

            while (IsScreenFadingIn())
            {
                await Delay(1);
            }

            FreezePlayer(PlayerId(), false);

            _spawnLock = false;

            TriggerEvent("playerSpawned", PlayerId());

            try
            {
                // create the vehicle 
                Vehicle vehicle = await World.CreateVehicle(new Model(2006667053)/*new Model(2071877360)*/, Game.PlayerPed.Position, Game.PlayerPed.Heading);
                // set the player ped into the vehicle and driver seat
                Game.PlayerPed.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                API.SetVehicleDoorsLocked(API.GetVehiclePedIsIn(API.PlayerPedId(), false), 4);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OnPlayerSpawned] {ex}");
            }
        }

        //public static async void SpawnPlayer()
        //{
        //    // FiveM used this on C++ side, so this might be needed (GTA might fade out on load themselves)
        //    Screen.Fading.FadeIn(0);
        //    RequestCollisionAtCoord(InitialSpawnCoords.X, InitialSpawnCoords.Y, InitialSpawnCoords.Z);
        //    await Game.Player.ChangeModel(defaultModel);
        //    Game.PlayerPed.Position = InitialSpawnCoords;
        //    Game.PlayerPed.Heading = heading;
        //    ShutdownLoadingScreen();
        //    TriggerEvent("playerSpawned");
        //}
    }

    public class Scoreboard : BaseScript
    {
        bool scoreboardVisible = false;
        PlayerList playerList = new PlayerList();
        Dictionary<int, DateTime> playerJoins = new Dictionary<int, DateTime>();
        DateTime? lastReboot = null;

        public Scoreboard()
        {
            EventHandlers["playerSpawned"] += new Action(() => { Debug.WriteLine("Requesting timestamps from server"); TriggerServerEvent("HardCap.RequestPlayerTimestamps"); });
            EventHandlers["playerConnecting"] += new Action<int>((serverId) => { Debug.WriteLine("A player is connecting; adding to scoreboard"); playerJoins.Add(serverId, DateTime.Now); });
            EventHandlers["playerDropped"] += new Action<int, string>((serverId, reason) =>
            {
                Debug.WriteLine("A player is discconnecting; removing from scoreboard");
                if (playerJoins.ContainsKey(serverId)) playerJoins.Remove(serverId);
            });
            //EventHandlers["Scoreboard.ReceivePlayerTimestamps"] += new Action<string, string>((dict, serializedTimeStamp) => {
            //    Debug.WriteLine("Received timestamps for scoreboard");
            //    var res = FamilyRP.Roleplay.Helpers.MsgPack.Deserialize<Dictionary<int, DateTime>>(dict);
            //    res.ToList().ForEach(i => { if (!playerJoins.ContainsKey(i.Key)) playerJoins.Add(i.Key, i.Value.ToLocalTime()); });
            //    lastReboot = FamilyRP.Roleplay.Helpers.MsgPack.Deserialize<DateTime>(serializedTimeStamp).ToLocalTime();
            //});

            Tick += OnTick;
        }

        private Task OnTick()
        {
            try
            {
                if (Game.IsControlPressed(0, Control.ReplayStartStopRecording))
                {
                    if (!scoreboardVisible)
                    {

                        scoreboardVisible = true;
                        string Json = $@"{{""text"": ""{String.Join("", playerList.Select(player => $@"<tr class=\""playerRow\""><td class=\""playerId\"">{player.ServerId}</td><td class=\""playerName\"">{player.Name.Replace(@"'", @"&apos;").Replace(@"""", @"&quot;")}</td><td class=\""playerTime\"">{(playerJoins.ContainsKey(player.ServerId) ? $@"{(DateTime.Now.Subtract(playerJoins[player.ServerId]).Hours > 0 ? $"{DateTime.Now.Subtract(playerJoins[player.ServerId]).Hours}h {DateTime.Now.Subtract(playerJoins[player.ServerId]).Minutes}h" : $"{DateTime.Now.Subtract(playerJoins[player.ServerId]).Minutes}m")}" : "")}</td></tr>"))}""";
                        if (lastReboot != null)
                            Json += $@", ""uptime"": ""{(DateTime.Now.Subtract((DateTime)lastReboot).Hours > 0 ? $"{DateTime.Now.Subtract((DateTime)lastReboot).Hours}h {DateTime.Now.Subtract((DateTime)lastReboot).Minutes}h" : $"{DateTime.Now.Subtract((DateTime)lastReboot).Minutes}m")}""}}";
                        else
                            Json += $@", ""uptime"": """"}}";
                        SendNuiMessage(Json);
                        Debug.WriteLine("Sent JSON to Scoreboard");
                    }
                }
                else
                {
                    if (scoreboardVisible)
                    {
                        scoreboardVisible = false;
                        string Html = $@"{{""meta"": ""close""}}";
                        SendNuiMessage(Html);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Scoreboard Error: {ex.Message}");
            }
            return Task.FromResult(0);
        }

        // Temporary
        public static string CleanForJSON(string s)
        {
            if (s == null || s.Length == 0)
            {
                return "";
            }

            char c = '\0';
            int i;
            int len = s.Length;
            StringBuilder sb = new StringBuilder(len + 4);
            String t;

            for (i = 0; i < len; i += 1)
            {
                c = s[i];
                switch (c)
                {
                    case '\\':
                    case '"':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '/':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        if (c < ' ')
                        {
                            t = "000" + String.Format("X", c);
                            sb.Append("\\u" + t.Substring(t.Length - 4));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
