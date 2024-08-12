namespace TheBackrooms;

sealed class Warpper
{
    public void WarpIntoBK(RainWorldGame game)
    {
        World origin_world = game.overWorld.activeWorld;
        UnityEngine.Debug.Log("activeWorld: " + origin_world.name);

        game.overWorld.LoadWorld("BK", game.overWorld.PlayerCharacterNumber, false);
        World bk_world = game.overWorld.activeWorld;
        UnityEngine.Debug.Log("activeWorld: " + bk_world.name);

        AbstractRoom bk_room = bk_world.GetAbstractRoom("BK_A001");
        if (game.roomRealizer != null)
        {
            game.roomRealizer = new RoomRealizer(game.roomRealizer.followCreature, bk_world);
        }
        bk_room.RealizeRoom(bk_world, game);

        foreach (AbstractCreature player in game.Players)
        {
            if (player.realizedCreature.room != null)
            {
                player.realizedCreature.room.RemoveObject(player.realizedCreature);
                (player.realizedCreature as Player).objectInStomach.world = bk_world;
            }
            player.world = bk_world;
            WorldCoordinate newPos = new WorldCoordinate(bk_room.index, 36, 16, -1);
            player.pos = newPos;
            bk_world.GetAbstractRoom(newPos).AddEntity(player);
            player.realizedCreature.PlaceInRoom(bk_room.realizedRoom);
            foreach (RoomCamera camera in game.cameras)
            {
                camera.virtualMicrophone.AllQuiet();
                camera.MoveCamera(bk_room.realizedRoom, 0);;
            }
        }
    }
}
