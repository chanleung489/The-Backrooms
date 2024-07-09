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

    //private static readonly CreatureTemplate.Type TRAIN_TYPE = MoreSlugcatsEnums.CreatureTemplateType.TrainLizard;
    //private static readonly CreatureTemplate.Type RED_CENTI_TYPE = CreatureTemplate.Type.RedCentipede;
    private static readonly int BK_CENTER_ROOM_INDEX = 87;

    private AbstractCreature pursuer;
    private Player targetPlayer;
    private WorldCoordinate destination;
    private string currentRoom;
    private int[] logCooldowns = new int[16];
    private bool[] logFlags = new bool[16];
    private string logString = "";

    bool init;

    public void OnEnable()
    {
        On.RainWorld.OnModsInit += OnModsInit;
        On.RainWorldGame.Update += OnGameUpdate;
    }

    public void OnDisable()
    {
        On.RainWorld.OnModsInit -= OnModsInit;
        On.RainWorldGame.Update -= OnGameUpdate;
    }

    private void LogOnce(object data, bool once)
    {

        if (logFlags[0] && once) return;

        UnityEngine.Debug.Log(data);
        logFlags[0] = true;
    }

    private void LogTimed(int time, int index, string logs)
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

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (init) return;

        init = true;

        Logger.LogDebug("Init");
    }

    private void OnGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);

        if (self.world == null) return;
        if (self.world.name != "BK") return;

        LogTimed(360, 1, logString);
        logString = "";
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
                }
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
            UnityEngine.Debug.Log("Pursuer moving to: " + this.currentRoom);
            this.currentRoom = this.pursuer.Room.name;
        }
        if (this.pursuer.abstractAI.destination != this.destination)
        {
            this.destination = this.targetPlayer.abstractCreature.pos;
            this.pursuer.abstractAI.SetDestination(this.destination);
            //self.cameras[0].hud.textPrompt.AddMessage(Expedition.ChallengeTools.IGT.Translate("You are being pursued..."), 10, 250, true, true);
        }
    }
}
