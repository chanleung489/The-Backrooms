using BepInEx;
using MoreSlugcats;
using System;
using System.Collections.Generic;
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
    public const string PLUGIN_VERSION = "1.0";

    static readonly int BK_CENTER_ROOM_INDEX = 87;

    AbstractCreature pursuer;
    Player targetPlayer;
    WorldCoordinate destination;
    string currentRoom;
    bool pursuerDead;
    bool warping;
    int clippedTimer = 0;
    Warper warper;
    FadeOut fadeOut;

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
    }

    public void OnDisable()
    {
        On.RainWorld.OnModsInit -= OnModsInit;
        On.RainWorldGame.Update -= OnGameUpdate;
        On.AbstractSpaceVisualizer.ChangeRoom -= OnChangeRoom;
        On.World.LoadWorld -= OnLoadWorld;
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
        foreach (var log in logs.Split('#'))
        {
            UnityEngine.Debug.Log(log);
            Logger.LogDebug(log);
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

    void FadeOutForEveryone (RainWorldGame game, bool fadeIn)
    {
        foreach (AbstractCreature player in game.NonPermaDeadPlayers)
        {
            if (!game.cameras[0].InCutscene)
            {
                game.cameras[0].EnterCutsceneMode(player, RoomCamera.CameraCutsceneType.EndingOE);
            }
            if (fadeOut == null)
            {
                fadeOut = new FadeOut(player.Room.realizedRoom, Color.black, 60f, fadeIn);
                player.Room.realizedRoom.AddObject(fadeOut);
            }
        }
    }

    void WarpOnClipping(RainWorldGame game)
    {
        if (warping)
        {
            FadeOutForEveryone(game, fadeIn: false);
            if (fadeOut != null && fadeOut.IsDoneFading() && warper == null)
            {
                warper = new Warper();
                warper.WarpIntoBK(game);
                fadeOut = null;
                warping = false;

                foreach (AbstractCreature player in game.NonPermaDeadPlayers)
                {
                    player.realizedCreature.Stun(120);
                }
                FadeOutForEveryone(game, fadeIn: true);
                UnityEngine.Debug.Log("fading in");
                game.cameras[0].ExitCutsceneMode();
            }
            return;
        }

        if (targetPlayer.inShortcut) return;
        if (!targetPlayer.GoThroughFloors)
        {
            clippedTimer = 0;
            return;
        }
        clippedTimer += 1;
        if (clippedTimer % 40 == 0) UnityEngine.Debug.Log(clippedTimer); 
        if (clippedTimer < 150) return;

        warping = true;

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

        if (targetPlayer == null)
        {
            for (int i = 0; i < self.Players.Count; i++)
            {
                if (self.Players[i] != null && self.Players[i].realizedCreature != null && !self.Players[i].realizedCreature.dead)
                {
                    targetPlayer = (self.Players[i].realizedCreature as Player);
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
}
