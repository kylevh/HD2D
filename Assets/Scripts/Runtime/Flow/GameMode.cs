namespace KVH.Game.Flow
{
    // Overworld vs menus vs encounter. Battle code lives in Combat/, not inside PlayerMotor.
    public enum GameMode
    {
        Exploration = 0,
        Menu = 1,
        Battle = 2,
    }
}
