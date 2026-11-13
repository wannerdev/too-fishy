using UnityEngine;

namespace TooFishy
{
    /// <summary>Physics layers (ProjectSettings/TagManager.asset).</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Fish = 8;
        public const int Player = 9;
        public const int World = 10;
        public const int Projectile = 11;
        /// <summary>Player areas that only detect fish (ScatterArea).</summary>
        public const int PlayerSensor = 12;

        /// <summary>
        /// Godot resolves submarine/fish contacts by pushing the fish out of the hull, so the
        /// CharacterController must not stop at fish; fish handle the overlap themselves
        /// (FishBehaviour.ResolveCollisions). Sensors only see fish.
        /// </summary>
        public static void Configure()
        {
            Physics.IgnoreLayerCollision(Player, Fish, true);
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(PlayerSensor, i, i != Fish);
        }
    }
}
