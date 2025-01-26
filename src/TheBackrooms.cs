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
    public const string PLUGIN_VERSION = "1.2.1";

    const int SECOND = 40;
    const int MUSHROOM_DURATION = 320;
    const int BK_CENTER_ROOM_INDEX = 87;

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

        if (!BackroomsOptions.showWarning.Value || self.room == null || shownRoomWarning) return;

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
            FadeOut fadeout = new FadeOut(room, Color.black, 2 * SECOND, fadeIn);
            fadeouts.Add(fadeout);
            player.Room.realizedRoom.AddObject(fadeout);
        }
    }

    void WarpOnClipping(RainWorldGame game)
    {
        if (warping)
        {
            fadeoutForAll(game);
            if (!fadeouts.All(x => x.IsDoneFading())) return;
            fadeouts.Clear();

            RegionSwitcher warper = new RegionSwitcher();
            warper.SwitchRegions(game, "BK", "BK_A001", new IntVector2(0, 0));

            foreach (AbstractCreature abstractPlayer in game.NonPermaDeadPlayers)
            {
                Player player = abstractPlayer.realizedCreature as Player;
                player.Stun(3 * SECOND);
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

        foreach (AbstractCreature abstractPlayer in game.NonPermaDeadPlayers)
        {
            Player player = abstractPlayer.realizedCreature as Player;
            if (player.room == null) continue;
            IntVector2 playerTilePos = player.room.GetTilePosition((targetPlayer.mainBodyChunk.pos.y < targetPlayer.bodyChunks[1].pos.y) ? targetPlayer.mainBodyChunk.pos : targetPlayer.bodyChunks[1].pos);
            if (!player.GoThroughFloors || targetPlayer.room.GetTile(playerTilePos).Solid == false)
            {
                clippedTimer = 0;
                return;
            }
            if (BackroomsOptions.warpOnAny.Value) break;
        }
        clippedTimer += 1;
        if (targetPlayer.mushroomCounter > 0) clippedTimer += 1;
        if (clippedTimer % SECOND == 0) UnityEngine.Debug.Log(clippedTimer);
        if (clippedTimer < BackroomsOptions.prewarpDuration.Value * SECOND) return;

        warping = true;
        LogBoth("warping");
        game.world.game.cameras[0].hud.textPrompt.AddMessage("warping... (please do NOT press any buttons during the warp)", 10, 250, false, true);

    }

    void PursuePlayer(RainWorldGame game)
    {
        logString += $"region is bk, bk has {game.world.NumberOfRooms} rooms #";

        if (pursuer == null)
        {
            AbstractRoom centerRoom = game.world.GetAbstractRoom(BK_CENTER_ROOM_INDEX + game.world.firstRoomIndex);

            if (centerRoom?.creatures.Count <= 0) return;
            logString += $"room {centerRoom.name} has creatures #";

            for (int j = 0; j < centerRoom.creatures.Count; j++)
            {
                logString += $"creature {j} is {centerRoom.creatures[j].creatureTemplate.type} #";
                if (centerRoom.creatures[j].creatureTemplate.type == MoreSlugcatsEnums.CreatureTemplateType.TrainLizard)
                {
                    pursuer = centerRoom.creatures[j];
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

        if (pursuer.abstractAI?.RealAI == null)
        {
            pursuer.Room.RealizeRoom(game.world, game);
            return;
        }
        pursuer.abstractAI.RealAI.tracker?.SeeCreature(targetPlayer.abstractCreature);
        logString += $"pursuer agression: {pursuer.abstractAI.RealAI.CurrentPlayerAggression(targetPlayer.abstractCreature)} #";

        if (currentRoom != pursuer.Room.name)
        {
            UnityEngine.Debug.Log("Pursuer moving from: " + currentRoom + " to " + pursuer.Room.name);
            currentRoom = pursuer.Room.name;
        }

        if (destination.room != pursuer.pos.room)
        {
            destination = targetPlayer.abstractCreature.pos;
            pursuer.abstractAI.SetDestination(destination);
        }

        if (shownWarning || !BackroomsOptions.showWarning.Value) return;
        foreach (int connection in pursuer.Room.connections)
        {
            if (connection != targetPlayer.abstractCreature.pos.room) continue;
            game.world.game.cameras[0].hud.textPrompt.AddMessage("DONT MOVE STAY STILL", 10, 250, true, true);
            shownWarning = true;
        }

    }

    void OnGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);
        LogTimed(12 * SECOND, 1, logString);
        logString = "#";

        logString += $"danger level: {BackroomsOptions.dangerlevel.Value} pursuer dead: {pursuerDead} #";

        if (self.world == null) return;

        if (targetPlayer == null)
        {
            foreach (AbstractCreature abstractPlayer in self.AlivePlayers)
            {
                if (abstractPlayer?.realizedCreature is Player player)
                {
                    if (!player.dead) targetPlayer = player;
                    break;
                }
            }
            return;
        }

        if (self.world.name != "BK")
        {
            if (BackroomsOptions.noclipWarp.Value) WarpOnClipping(self);
        }

        foreach (AbstractCreature abstractPlayer in self.Players)
        {
            Player player = (abstractPlayer?.realizedCreature as Player);
            if (player.mushroomCounter <= MUSHROOM_DURATION - BackroomsOptions.noclipDuration.Value * SECOND)
            {
                player.CollideWithTerrain = true;
            }
        }

        logString += "scary warning: " + BackroomsOptions.showWarning.Value;

        if (BackroomsOptions.dangerlevel.Value == 2 || pursuerDead || self.world.name != "BK") return;
        PursuePlayer(self);

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
