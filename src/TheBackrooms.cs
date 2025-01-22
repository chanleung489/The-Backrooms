using BepInEx;
using MoreSlugcats;
using RWCustom;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using UnityEngine;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace TheBackrooms;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
sealed class BackroomsMain : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "znery.backrooms";
    public const string PLUGIN_NAME = "The Backrooms";
    public const string PLUGIN_VERSION = "1.2";

    static readonly int BK_CENTER_ROOM_INDEX = 87;

    AbstractCreature pursuer;
    Player targetPlayer;
    WorldCoordinate destination;
    string currentRoom;
    bool pursuerDead;
    bool warping;
    int clippedTimer = 0;
    HashSet<FadeOut> fadeouts = new HashSet<FadeOut>();

    int[] logCooldowns = new int[16];
    bool[] logFlags = new bool[16];
    string logString = "";

    bool shownRoomWarning = false;
    bool shownWarning = false;

    bool init;

    public void OnEnable()
    {
        On.RainWorld.OnModsInit += OnModsInit;
        On.RainWorldGame.Update += OnGameUpdate;
        On.AbstractSpaceVisualizer.ChangeRoom += OnChangeRoom;
        On.World.LoadWorld += OnLoadWorld;
        On.Mushroom.BitByPlayer += OnEatMushroom;
        // On.Options.ControlSetup.GetAxis += OnGetAxis;
    }

    private float OnGetAxis(On.Options.ControlSetup.orig_GetAxis orig, Options.ControlSetup self, int actionID)
    {
        float result = orig(self, actionID);
        LogBoth($"axis {actionID}: {result}");
        return result;
    }

    void LogBoth(string log)
    {
        UnityEngine.Debug.Log(log);
        Logger.LogDebug(log);
    }

    void LogTimed(int time, int index, string logs)
    {
        logCooldowns[index]++;
        if (logCooldowns[index] >= time)
        {
            logFlags[index] = false;
            logCooldowns[index] = 0;
        }
        if (logFlags[index])
        {
            return;
        }
        foreach (string log in logs.Split('#'))
        {
            LogBoth(log);
        }
        logFlags[index] = true;
    }

    void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        MachineConnector.SetRegisteredOI(PLUGIN_GUID, new BackroomsOptions());

        if (init) return;
        init = true;
        Logger.LogDebug("Init");
    }

    void OnLoadWorld(On.World.orig_LoadWorld orig, World self, SlugcatStats.Name slugcatNumber, List<AbstractRoom> abstractRoomsList, int[] swarmRooms, int[] shelters, int[] gates)
    {
        orig(self, slugcatNumber, abstractRoomsList, swarmRooms, shelters, gates);

        pursuer = null;
        targetPlayer = null;
        currentRoom = null;
        pursuerDead = false;
        shownRoomWarning = false;
        shownWarning = false;

        Logger.LogDebug("Load world");
    }

    void OnChangeRoom(On.AbstractSpaceVisualizer.orig_ChangeRoom orig, AbstractSpaceVisualizer self, Room newRoom)
    {
        orig(self, newRoom);

        if (self.room == null) return;
        if (shownRoomWarning) return;

        if (self.room.abstractRoom == self.world.GetAbstractRoom(BK_CENTER_ROOM_INDEX + self.world.firstRoomIndex))
        {
            self.world.game.cameras[0].hud.textPrompt.AddMessage("DONT MOVE STAY STILL", 10, 250, true, true);
            shownRoomWarning = true;
        }
    }

    void fadeoutForAll(RainWorldGame game, bool fadeIn = false)
    {
        foreach (AbstractCreature player in game.NonPermaDeadPlayers)
        {
            if (!game.cameras[0].InCutscene)
            {
                game.cameras[0].EnterCutsceneMode(player, RoomCamera.CameraCutsceneType.EndingOE);
            }
            Room room = player.Room.realizedRoom;
            if (room.drawableObjects.Any(x => x is FadeOut)) continue;
            FadeOut fadeout = new FadeOut(room, Color.black, 60f, fadeIn);
            fadeouts.Add(fadeout);
            player.Room.realizedRoom.AddObject(fadeout);
        }
    }

    string RandomRoom(AbstractRoom[] rooms)
    {
        string destRoom = "BK_A001";
        for (int i = 0; i < 10; i++)
        {
            destRoom = rooms[Random.Range(0, rooms.Length)].name;
            if (destRoom.Contains("_A")) return destRoom;
        }
        return "BK_A001";
    }

    void WarpOnClipping(RainWorldGame game)
    {
        if (warping)
        {
            fadeoutForAll(game);
            if (!fadeouts.All(x => x.IsDoneFading())) return;
            fadeouts.Clear();

            RegionSwitcher warper = new RegionSwitcher();
            string destRoom = RandomRoom(game.world.abstractRooms);
            LogBoth($"dest {destRoom}");
            warper.SwitchRegions(game, "BK", "BK_A001", new IntVector2(0, 0));

            foreach (AbstractCreature abstractPlayer in game.NonPermaDeadPlayers)
            {
                Player player = abstractPlayer.realizedCreature as Player;
                player.Stun(120);
                player.CollideWithTerrain = true;
                foreach (BodyChunk bodyChunk in player.bodyChunks)
                {
                    bodyChunk.vel = Custom.DegToVec(UnityEngine.Random.value * 360f) * 12f;
                    bodyChunk.pos = new Vector2(460, 480);
                    bodyChunk.lastPos = new Vector2(460, 480);
                }
            }
            fadeoutForAll(game, fadeIn: true);
            fadeouts.Clear();
            LogBoth("fading in");
            game.cameras[0].ExitCutsceneMode();
            game.cameras[0].virtualMicrophone.AllQuiet();
            warping = false;
            return;
        }

        if (targetPlayer.room == null) return;
        IntVector2 playerTilePos = targetPlayer.room.GetTilePosition((targetPlayer.mainBodyChunk.pos.y < targetPlayer.bodyChunks[1].pos.y) ? targetPlayer.mainBodyChunk.pos : targetPlayer.bodyChunks[1].pos);
        if (!targetPlayer.GoThroughFloors || targetPlayer.room.GetTile(playerTilePos).Solid == false)
        {
            clippedTimer = 0;
            return;
        }
        clippedTimer += 1;
        if (targetPlayer.mushroomCounter > 0) clippedTimer += 1;
        if (clippedTimer % 40 == 0) UnityEngine.Debug.Log(clippedTimer);
        if (clippedTimer < 200) return;

        warping = true;
        LogBoth("warping");

    }

    void OnGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);
        LogTimed(480, 1, logString);
        logString = "#";

        logString += $"danger level: {BackroomsOptions.dangerlevel.Value} pursuer dead: {pursuerDead} #";

        if (BackroomsOptions.dangerlevel.Value == 2) return;
        if (pursuerDead) return;

        if (self.world == null) return;

        foreach (AbstractCreature abstractPlayer in self.Players)
        {
            Player player = (abstractPlayer?.realizedCreature as Player);
            if (player.mushroomCounter <= 300)
            {
                player.CollideWithTerrain = true;
            }
        }

        if (targetPlayer == null)
        {
            foreach (AbstractCreature abstractPlayer in self.AlivePlayers)
            {
                if (abstractPlayer?.realizedCreature is Player player)
                {
                    targetPlayer = player;
                }
            }
            return;
        }
        if (self.world.name != "BK")
        {
            WarpOnClipping(self);
            return;
        }

        logString += $"region is bk, bk has {self.world.NumberOfRooms} rooms #";

        if (pursuer == null)
        {
            AbstractRoom abstractRoom = self.world.GetAbstractRoom(BK_CENTER_ROOM_INDEX + self.world.firstRoomIndex);
            if (abstractRoom == null)
            {
                return;
            }
            if (abstractRoom.creatures.Count <= 0)
            {
                return;
            }
            logString += $"room {abstractRoom.name} has creatures #";
            for (int j = 0; j < abstractRoom.creatures.Count; j++)
            {
                logString += $"creature {j} is {abstractRoom.creatures[j].creatureTemplate.type} #";
                if (abstractRoom.creatures[j].creatureTemplate.type == MoreSlugcatsEnums.CreatureTemplateType.TrainLizard)
                {
                    pursuer = abstractRoom.creatures[j];
                    break;
                }
            }
            if (BackroomsOptions.dangerlevel.Value == 1)
            {
                pursuer.Die();
                pursuerDead = pursuer.state.dead;
            }
            return;
        }
        if (pursuer.state.dead) return;
        logString += $"pursuer is {pursuer} #";

        if (pursuer.abstractAI == null) return;
        if (pursuer.abstractAI.RealAI == null)
        {
            logString += "pursuer realai is null #";
            pursuer.Room.RealizeRoom(self.world, self);
            return;
        }
        if (pursuer.abstractAI.RealAI.tracker == null)
        {
            logString += "pursuer tracker is null #";
            return;
        }
        pursuer.abstractAI.RealAI.tracker.SeeCreature(targetPlayer.abstractCreature);
        logString += $"pursuer sees player, pursuer agression: {pursuer.abstractAI.RealAI.CurrentPlayerAggression(targetPlayer.abstractCreature)} #";
        if (currentRoom != pursuer.Room.name)
        {
            UnityEngine.Debug.Log("Pursuer moving from: " + currentRoom + " to " + pursuer.Room.name);
            currentRoom = pursuer.Room.name;
        }
        if (pursuer.abstractAI.destination != destination)
        {
            destination = targetPlayer.abstractCreature.pos;
            pursuer.abstractAI.SetDestination(destination);
        }

        if (!BackroomsOptions.scaryWarning.Value)
        {
            logString += "scary warning: " + BackroomsOptions.scaryWarning.Value;
            return;
        }
        if (shownWarning) return;
        foreach (int connection in pursuer.Room.connections)
        {
            if (connection != targetPlayer.abstractCreature.pos.room || pursuer.abstractAI.destination != destination) continue;
            self.world.game.cameras[0].hud.textPrompt.AddMessage("DONT MOVE STAY STILL", 10, 250, true, true);
            shownWarning = true;
        }

    }

    private void OnEatMushroom(On.Mushroom.orig_BitByPlayer orig, Mushroom self, Creature.Grasp grasp, bool eu)
    {
        float randomNumber = UnityEngine.Random.value;
        if (BackroomsOptions.noclipMushroomChance.Value >= randomNumber)
        {
            (grasp.grabber as Player).CollideWithTerrain = false;
        }
        UnityEngine.Debug.Log($"shroom noclip {BackroomsOptions.noclipMushroomChance.Value} >= {randomNumber}");
        orig(self, grasp, eu);
    }

}
