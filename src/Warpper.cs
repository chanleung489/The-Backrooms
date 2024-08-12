using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TheBackrooms;

sealed class Warpper
{
    bool firstPlayer = true;
    private MethodInfo _OverWorld_LoadWorld = typeof(OverWorld).GetMethod("LoadWorld", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    public void WarpIntoBK(RainWorldGame game)
    {
        World origin_world = game.overWorld.activeWorld;
        UnityEngine.Debug.Log("activeWorld: " + origin_world.name);
        AbstractRoom origin_room = origin_world.GetAbstractRoom(game.Players[0].pos);

        //game.overWorld.LoadWorld("BK", game.overWorld.PlayerCharacterNumber, false);
        _OverWorld_LoadWorld.Invoke(game.overWorld, new object[] { "BK", game.overWorld.PlayerCharacterNumber, false });
        World bk_world = game.overWorld.activeWorld;
        UnityEngine.Debug.Log("activeWorld: " + bk_world.name);

        AbstractRoom bk_room = bk_world.GetAbstractRoom("BK_A001");
        UnityEngine.Debug.Log("bk abstract room: " + bk_room.name);

        if (game.roomRealizer != null)
        {
            game.roomRealizer = new RoomRealizer(game.roomRealizer.followCreature, bk_world);
        }
        bk_room.RealizeRoom(bk_world, game);

        while (bk_world.loadingRooms.Count > 0)
        {
            for (int j = bk_world.loadingRooms.Count - 1; j >= 0; j--)
            {
                if (bk_world.loadingRooms[j].done)
                {
                    bk_world.loadingRooms.RemoveAt(j);
                }
                else
                {
                    bk_world.loadingRooms[j].Update();
                }
            }
        }
        UnityEngine.Debug.Log("bk realized room: " + bk_room.realizedRoom);

        int abstractNode = -1;
        for (int k = 0; k < bk_room.nodes.Length; k++)
        {
            if (bk_room.nodes[k].type == AbstractRoomNode.Type.Exit && k < bk_room.connections.Length && bk_room.connections[k] > -1)
            {
                abstractNode = k;
                break;
            }
        }

        foreach (AbstractCreature player in game.Players)
        {
            if (player.realizedCreature.room != null)
            {
                player.realizedCreature.room.RemoveObject(player.realizedCreature);
            }
            origin_room.RemoveEntity(player);

            player.world = bk_world;
            WorldCoordinate newPos = new WorldCoordinate(bk_room.index, 25, 23, abstractNode);
            player.pos = newPos;
            UnityEngine.Debug.Log("player pos: " + player.pos);

            bk_room.realizedRoom.aimap.NewWorld(bk_room.index);

            if (player.GetAllConnectedObjects != null)
            {
                foreach (AbstractPhysicalObject connectedObject in player.GetAllConnectedObjects()) {
                    connectedObject.world = bk_world;
                    connectedObject.pos = player.pos;
                    connectedObject.Room.RemoveEntity(connectedObject);
                    bk_room.AddEntity(connectedObject);
                    connectedObject.realizedObject.sticksRespawned = true;
                }
            }

            if (player.realizedCreature != null && (player.realizedCreature as Player).objectInStomach != null)
            {
                (player.realizedCreature as Player).objectInStomach.world = bk_world;
            }

            bk_world.GetAbstractRoom(newPos).AddEntity(player);
            player.realizedCreature.PlaceInRoom(bk_room.realizedRoom);
            player.Move(newPos);
            //player.Move(bk_room.realizedRoom.LocalCoordinateOfNode(0));

            UnityEngine.Debug.Log("moved player");

            player.RealizeInRoom();

            if (player.creatureTemplate.AI)
            {
                player.abstractAI.NewWorld(bk_world);
                player.InitiateAI();
                player.abstractAI.RealAI.NewRoom(bk_room.realizedRoom);
            }

            if (firstPlayer)
            {
                bk_room.world.game.roomRealizer.followCreature = player;
            }

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
        game.cameras[0].MoveCamera(bk_room.realizedRoom, 0);
        game.cameras[0].FireUpSinglePlayerHUD(game.AlivePlayers[0].realizedCreature as Player);

        foreach (RoomCamera camera in game.cameras)
        {
            camera.hud.ResetMap(new HUD.Map.MapData(bk_world, game.rainWorld));
            camera.dayNightNeedsRefresh = true;
            if (camera.hud.textPrompt.subregionTracker != null)
            {
                camera.hud.textPrompt.subregionTracker.lastShownRegion = 0;
            }
        }

        UnityEngine.Debug.Log("done warpping");
    }
}
