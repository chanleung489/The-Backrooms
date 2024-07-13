using BepInEx;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Security.Permissions;

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

    void OnGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);
        LogTimed(480, 1, logString);
        logString = "";

        logString += $"danger level: {BackroomsOptions.dangerlevel.Value} pursuer dead {pursuerDead} #";

        if (BackroomsOptions.dangerlevel.Value == 2) return;
        if (pursuerDead) return;

        if (self.world == null) return;
        if (self.world.name != "BK") return;

        logString += $"region is bk, bk has {self.world.NumberOfRooms} rooms #";

        if (this.pursuer == null)
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
                    this.pursuer = abstractRoom.creatures[j];
                    break;
                }
            }
            if (BackroomsOptions.dangerlevel.Value == 1)
            {
                this.pursuer.Die();
                pursuerDead = this.pursuer.state.dead ? true : false;
            }
            return;
        }
        if (this.pursuer.state.dead) return;
        logString += $"pursuer is {this.pursuer} #";

        if (this.targetPlayer == null)
        {
            for (int i = 0; i < self.Players.Count; i++)
            {
                if (self.Players[i] != null && self.Players[i].realizedCreature != null && !self.Players[i].realizedCreature.dead)
                {
                    this.targetPlayer = (self.Players[i].realizedCreature as Player);
                }
            }
        }

        if (this.pursuer.abstractAI == null) return;
        if (this.pursuer.abstractAI.RealAI == null)
        {
            logString += "pursuer realai is null #";
            this.pursuer.Room.RealizeRoom(self.world, self);
            return;
        }
        if (this.pursuer.abstractAI.RealAI.tracker == null)
        {
            logString += "pursuer tracker is null #";
            return;
        }
        this.pursuer.abstractAI.RealAI.tracker.SeeCreature(this.targetPlayer.abstractCreature);
        logString += $"pursuer sees player, pursuer agression: {this.pursuer.abstractAI.RealAI.CurrentPlayerAggression(this.targetPlayer.abstractCreature)} #";
        if (this.currentRoom != this.pursuer.Room.name)
        {
            UnityEngine.Debug.Log("Pursuer moving from: " + this.currentRoom + " to " + this.pursuer.Room.name);
            this.currentRoom = this.pursuer.Room.name;
        }
        if (this.pursuer.abstractAI.destination != this.destination)
        {
            this.destination = this.targetPlayer.abstractCreature.pos;
            this.pursuer.abstractAI.SetDestination(this.destination);
        }
        if (shownWarning) return;
        foreach (int connection in this.pursuer.Room.connections)
        {
            if (connection != this.targetPlayer.abstractCreature.pos.room || this.pursuer.abstractAI.destination != this.destination) continue;
            self.world.game.cameras[0].hud.textPrompt.AddMessage("DONT MOVE STAY STILL", 10, 250, true, true);
            shownWarning = true;
        }

    }
}
