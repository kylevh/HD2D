namespace KVH.Game.Player
{
    // Dialogue, menus, cutscenes, and battle freeze walking. They set this on the motor.
    public enum PlayerBusy
    {
        Free = 0,
        Talking = 1,
        Menu = 2,
        Cutscene = 3,
        Battle = 4,
    }
}
