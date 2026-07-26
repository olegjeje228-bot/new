namespace EventHUD.SpecItems
{
    using System;
    using UnityEngine;

    public sealed class NoPhysicsProjectile : MonoBehaviour
    {
        private static readonly string[] IgnoredLayers = new string[]
        {
            "Pickup", "Pickups", "Item", "Items", "Ragdoll", "Ragdolls", "Hitbox", "Grenade",
        };

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            int mask = 0;

            foreach (string name in IgnoredLayers)
            {
                int layer = LayerMask.NameToLayer(name);

                if (layer >= 0)
                    mask |= 1 << layer;
            }

            if (mask != 0)
                FallbackIgnore(mask);
        }

        private void FallbackIgnore(int mask)
        {
            try
            {
                Collider mine = GetComponent<Collider>();

                if (mine is null)
                    return;

                Collider[] around = Physics.OverlapSphere(transform.position, 6f, mask);

                foreach (Collider other in around)
                {
                    if (!(other is null) && other != mine)
                        Physics.IgnoreCollision(mine, other, true);
                }
            }
            catch
            {
            }
        }
    }
}