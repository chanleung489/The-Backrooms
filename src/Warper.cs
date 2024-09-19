using On;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheBackrooms;

sealed class Warper
{
    World originWorld;
    World bkWorld;
    AbstractRoom originRoom;
    AbstractRoom bkRoom;
    bool firstPlayer = true;
    Spear spear;

    void LoadAllLoadingRooms(World world)
    {
        while (world.loadingRooms.Count > 0)
        {
            for (int i = world.loadingRooms.Count - 1; i >= 0; i--)
            {
                if (world.loadingRooms[i].done)
                {
                    world.loadingRooms.RemoveAt(i);
                }
                else
                {
                    world.loadingRooms[i].Update();
                }
            }
        }
    }

    void TrackObjects(AbstractCreature player)
    {

        List<AbstractPhysicalObject> allConnectedObjects = player?.GetAllConnectedObjects();
        if (allConnectedObjects != null)
        {
            foreach (AbstractPhysicalObject obj in allConnectedObjects)
            {
                if (obj == null) continue;
                obj.world = bkWorld;
                obj.pos = bkRoom.realizedRoom.LocalCoordinateOfNode(0);
                obj.Room?.RemoveEntity(obj);
                bkRoom.AddEntity(obj);
                if (obj.realizedObject == null) continue;
                obj.realizedObject.sticksRespawned = true;
            }
        }

        if (player.realizedCreature is not Player realizedPlayer) return;

        if (realizedPlayer.objectInStomach != null)
        {
            realizedPlayer.objectInStomach.world = bkWorld;
        }

        spear = realizedPlayer.spearOnBack?.spear ?? null;

        if (realizedPlayer.grasps == null) return;
        for (int j = 0; j < realizedPlayer.grasps.Length; j++)
        {
            Player.Grasp grasp = realizedPlayer.grasps[j];
            if (grasp == null) continue;
            PhysicalObject physicalObject = grasp.grabbed;
            if (physicalObject == null) continue;

            if (!grasp.discontinued && physicalObject is Creature && !(physicalObject is Player || (physicalObject as Player).isSlugpup))
            {
                realizedPlayer.ReleaseGrasp(j);
            }
        }

        if (!firstPlayer) return;
        foreach (AbstractPhysicalObject obj in allConnectedObjects)
        {
            int count = 0;
            for (int i = 0; i < bkRoom.realizedRoom.updateList.Count; i++)
            {
                if (obj.realizedObject == bkRoom.realizedRoom.updateList[i])
                {
                    count++;
                }
                if (count > 1)
                {
                    bkRoom.realizedRoom.updateList.RemoveAt(i);
                }
            }
        }
    }

    void MoveObjects(AbstractCreature player)
    {
        if (player.realizedCreature is Player realizedPlayer && spear != null && realizedPlayer.spearOnBack != null && realizedPlayer.spearOnBack.spear != spear)
        {
            realizedPlayer.spearOnBack.SpearToBack(spear);
            realizedPlayer.abstractPhysicalObject.stuckObjects.Add(realizedPlayer.spearOnBack.abstractStick);
        }
    }

    void WarpPlayer(AbstractCreature player, int abstractNode)
    {
        if ((player.realizedCreature as Player).slugOnBack != null && (player.realizedCreature as Player).slugOnBack.HasASlug)
        {
            (player.realizedCreature as Player).slugOnBack.DropSlug();
        }
        player.realizedCreature.room?.RemoveObject(player.realizedCreature);
        originRoom.RemoveEntity(player);

        player.world = bkWorld;
        WorldCoordinate newPos = new WorldCoordinate(bkRoom.index, 24, 23, abstractNode);
        player.pos = newPos;
        UnityEngine.Debug.Log("player pos: " + player.pos);

        bkRoom.realizedRoom.aimap.NewWorld(bkRoom.index);

        TrackObjects(player);

        player.Move(newPos);
        UnityEngine.Debug.Log("moved player");

        player.RealizeInRoom();

        if (player.creatureTemplate.AI)
        {
            player.abstractAI.NewWorld(bkWorld);
            player.InitiateAI();
            player.abstractAI.RealAI.NewRoom(bkRoom.realizedRoom);
        }

        MoveObjects(player);

        bkRoom.world.game.roomRealizer.followCreature = player;

        if (firstPlayer)
        {
            firstPlayer = false;
        }
    }

    void SyncWorldStates()
    {
        originWorld.regionState.AdaptRegionStateToWorld(-1, bkRoom.index);
        if (originWorld.regionState != null)
        {
            originWorld.regionState.world = null;
        }
		bkWorld.rainCycle.baseCycleLength = originWorld.rainCycle.baseCycleLength;
		bkWorld.rainCycle.cycleLength = originWorld.rainCycle.cycleLength;
		bkWorld.rainCycle.timer = originWorld.rainCycle.timer;
		bkWorld.rainCycle.duskPalette = originWorld.rainCycle.duskPalette;
		bkWorld.rainCycle.nightPalette = originWorld.rainCycle.nightPalette;
		bkWorld.rainCycle.dayNightCounter = originWorld.rainCycle.dayNightCounter;
		if (ModManager.MSC)
		{
			if (originWorld.rainCycle.timer == 0)
			{
				bkWorld.rainCycle.preTimer = originWorld.rainCycle.preTimer;
				bkWorld.rainCycle.maxPreTimer = originWorld.rainCycle.maxPreTimer;
			}
			else
			{
				bkWorld.rainCycle.preTimer = 0;
				bkWorld.rainCycle.maxPreTimer = 0;
			}
		}
    }

    public void WarpIntoBK(RainWorldGame game)
    {
        originWorld = game.overWorld.activeWorld;
        originRoom = originWorld.GetAbstractRoom(game.Players[0].pos);

        game.overWorld.LoadWorld("BK", game.overWorld.PlayerCharacterNumber, false);

        bkWorld = game.overWorld.activeWorld;
        bkRoom = bkWorld.GetAbstractRoom("BK_A001");

        if (game.roomRealizer != null)
        {
            game.roomRealizer = new RoomRealizer(game.roomRealizer.followCreature, bkWorld);
        }
        bkRoom.RealizeRoom(bkWorld, game);

        LoadAllLoadingRooms(bkWorld);
        UnityEngine.Debug.Log("bk realized room: " + bkRoom.realizedRoom);

        int abstractNode = -1;
        for (int k = 0; k < bkRoom.nodes.Length; k++)
        {
            if (bkRoom.nodes[k].type == AbstractRoomNode.Type.Exit && k < bkRoom.connections.Length && bkRoom.connections[k] > -1)
            {
                abstractNode = k;
                break;
            }
        }

        foreach (AbstractCreature player in game.AlivePlayers)
        {
            WarpPlayer(player, abstractNode);
        }

        for (int n = game.shortcuts.transportVessels.Count - 1; n >= 0; n--)
        {
            if (!game.overWorld.activeWorld.region.IsRoomInRegion(game.shortcuts.transportVessels[n].room.index))
            {
                game.shortcuts.transportVessels.RemoveAt(n);
            }
        }
        for (int num = game.shortcuts.betweenRoomsWaitingLobby.Count - 1; num >= 0; num--)
        {
            if (!game.overWorld.activeWorld.region.IsRoomInRegion(game.shortcuts.betweenRoomsWaitingLobby[num].room.index))
            {
                game.shortcuts.betweenRoomsWaitingLobby.RemoveAt(num);
            }
        }
        for (int num2 = game.shortcuts.borderTravelVessels.Count - 1; num2 >= 0; num2--)
        {
            if (!game.overWorld.activeWorld.region.IsRoomInRegion(game.shortcuts.borderTravelVessels[num2].room.index))
            {
                game.shortcuts.borderTravelVessels.RemoveAt(num2);
            }
        }

        game.cameras[0].virtualMicrophone.AllQuiet();
        game.cameras[0].MoveCamera(bkRoom.realizedRoom, 0);
        game.cameras[0].FireUpSinglePlayerHUD(game.AlivePlayers[0].realizedCreature as Player);

        foreach (RoomCamera camera in game.cameras)
        {
            camera.hud.ResetMap(new HUD.Map.MapData(bkWorld, game.rainWorld));
            camera.dayNightNeedsRefresh = true;
            if (camera.hud.textPrompt.subregionTracker != null)
            {
                camera.hud.textPrompt.subregionTracker.lastShownRegion = 0;
            }
        }

        SyncWorldStates();

        UnityEngine.Debug.Log("done warping");
    }
}
