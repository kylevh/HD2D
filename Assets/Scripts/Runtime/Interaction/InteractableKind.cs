namespace KVH.Game.Interaction
{
    // How focus feedback should look. Pixel/JRPG common practice:
    // - Props/chests: gold X-ray outline + E
    // - NPCs: overhead Talk cue (no full-body outline — clutters character silhouette at low res)
    public enum InteractableKind
    {
        Prop = 0,
        Npc = 1,
    }
}
