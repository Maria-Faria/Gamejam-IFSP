namespace Roguelike
{
    /// How big the monsters are built in this game. Tools/make_monsters.py sizes every monster relative to a
    /// player character whose largest side is ReferenceSize (1.109 m): balls 0.5x, regular/ghost/3_eyed/claw
    /// 0.75x, ghoul/horned/crescent/muscle 1x, bosses fat 1.7x, muscle_boss 1.8x, skull_face 2x. Here the player
    /// (Annie Easley: annie_easley-compressed.obj, 1.105 m tall, placed at scale 1.63 under the Player in
    /// SampleScene) is PlayerSize tall, so everything about a monster — mesh (FBX import scale), colliders,
    /// reach, ranges, speeds — is multiplied by Factor to keep those ratios against this player.
    public static class MonsterScale
    {
        /// Largest side of the player character the monsters were modelled against (edna.fbx), m.
        public const float ReferenceSize = 1.109f;

        /// Height of this game's player (1.1053 m OBJ x 1.63), m. Change this if the player model changes,
        /// then re-run Roguelike/Build Monsters.
        public const float PlayerSize = 1.8016f;

        /// World scale applied to the monsters (≈1.62).
        public const float Factor = PlayerSize / ReferenceSize;
    }
}
