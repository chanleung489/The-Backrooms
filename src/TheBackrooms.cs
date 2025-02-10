using BepInEx;
using BepInEx.Logging;
using HUD;
using MoreSlugcats;
using RWCustom;
using System;
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
    public const string PLUGIN_VERSION = "1.2.2";

    const int SECOND = 40;
    const int MUSHROOM_DURATION = 320;
    const int BK_CENTER_ROOM_INDEX = 87;
    public static new ManualLogSource Logger;

    AbstractCreature pursuer;
    Player targetPlayer;
    WorldCoordinate destination;
    string currentRoom;
    bool pursuerDead;
    bool warping;
    static int clippedTimer = 0;
    HashSet<FadeOut> fadeouts = new HashSet<FadeOut>();

    int[] logCooldowns = new int[16];
    bool[] logFlags = new bool[16];
    string logString = "";

    bool shownRoomWarning = false;
    bool shownWarning = false;

    bool init;

    public static float warpProgress
    {
        get
        {
            return (float) clippedTimer / (BackroomsOptions.prewarpDuration.Value * SECOND);
        }
    }

    public void OnEnable()
    {
        Logger = base.Logger;
        On.RainWorld.OnModsInit += OnModsInit;
        On.RainWorldGame.Update += OnGameUpdate;
        On.AbstractSpaceVisualizer.ChangeRoom += OnChangeRoom;
        On.World.LoadWorld += OnLoadWorld;
        On.Mushroom.BitByPlayer += OnEatMushroom;
        On.HUD.HUD.InitSinglePlayerHud += OnInitHud;
    }

    public static void LogBoth(string log)
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
            foreach (FadeOut fadeout in fadeouts)
            {
                fadeout.Destroy();
            }
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
        if (targetPlayer.mushroomEffect > 0) clippedTimer += 1;
        if (clippedTimer % SECOND == 0) {
            // UnityEngine.Debug.Log(clippedTimer);
            LogBoth($"clippedTimer {clippedTimer}");
            LogBoth($"prewarpDuration {BackroomsOptions.prewarpDuration.Value * SECOND}");
            LogBoth($"warpProgress {warpProgress}");
        }
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

        if (BackroomsOptions.noclipWarp.Value && self.world.name != "BK" && !targetPlayer.inShortcut)
        {
            WarpOnClipping(self);
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

    private void OnInitHud(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
    {
        orig(self, cam);
        if (BackroomsOptions.progressMeter.Value) self.AddPart(new PreWarpProgressMeter(self, self.fContainers[1]));
    }

}

public class PreWarpProgressMeter : HudPart
{
    private float warpProgress = 0;

    private Vector2 pos;
    private Vector2 lastPos;
    private HUDCircle[] circles;
    private float fade;
    private float lastFade;
    private FContainer myContainer;
    private Player hudPlayer
    {
        get
        {
            return this.hud.owner as Player;
        }
    }

    private bool Show
    {
        get
        {
            return this.hudPlayer != null && !this.hudPlayer.abstractCreature.world.game.GameOverModeActive && !this.hudPlayer.dead;
        }
    }

    public PreWarpProgressMeter(HUD.HUD hud, FContainer fContainer) : base(hud)
    {
        this.circles = new HUDCircle[5];
        this.pos = new Vector2(hud.rainWorld.options.ScreenSize.x / 2f - (float)this.circles.Length * 21.6f / 2f, 40f);
        this.lastPos = this.pos;
        this.fade = 0f;
        this.lastFade = 0f;
        for (int i = 0; i < this.circles.Length; i++)
        {
            this.circles[i] = new HUDCircle(hud, HUDCircle.SnapToGraphic.smallEmptyCircle, fContainer, 0);
            this.circles[i].fade = 0f;
            this.circles[i].lastFade = 0f;
        }
        this.myContainer = fContainer;
    }

    public override void Update()
    {
        this.warpProgress = TheBackrooms.BackroomsMain.warpProgress;
        if (warpProgress <= 0f || warpProgress >= 1f)
        {
            this.fade = Mathf.Lerp(this.fade, 0f, 0.2f);
        }
        else
        {
            this.fade = Mathf.Lerp(this.fade, this.Show ? 1f : 0f, 0.2f);
        }
        this.lastPos = this.pos;
        this.lastFade = this.fade;

        FLabel label = new FLabel(Custom.GetFont(), "Warp Progress");
        this.myContainer.AddChild(label);
        label.alignment = FLabelAlignment.Left;
        label.x = this.pos.x + (float)(this.circles.Length + 1) * 21.6f / 2f + 10f + label.textRect.width / 2f;
        label.y = this.pos.y;
        label.alpha = this.fade;

        float num = 1f / (float)this.circles.Length;
        float num2 = Mathf.InverseLerp(1f, 0f, warpProgress);
        for (int i = 0; i < this.circles.Length; i++)
        {
            float value = num2 - num * (float)i;
            this.circles[i].Update();
            this.circles[i].thickness = Mathf.Lerp(6f, 1f, Mathf.InverseLerp(num, 0f, value));
            this.circles[i].fade = Mathf.Lerp(this.circles[i].fade, Mathf.InverseLerp(num * (float)i, num * ((float)i + 1f), this.fade), 0.1f);
            this.circles[i].snapGraphic = HUDCircle.SnapToGraphic.smallEmptyCircle;
            this.circles[i].snapRad = 0.45f;
            this.circles[i].snapThickness = 0.45f;
            this.circles[i].rad = Mathf.Lerp(0.1f, 5f, this.circles[i].fade);
            this.circles[i].pos = this.pos + new Vector2((float)i * 21.6f, 0f);
        }
    }

    public override void Draw(float timeStacker)
    {
        for (int i = 0; i < this.circles.Length; i++)
        {
            this.circles[i].Draw(timeStacker);
        }
    }
}
