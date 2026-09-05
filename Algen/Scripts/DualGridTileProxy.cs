using UnityEngine;

namespace AglenRealms.WorldCore
{
    [DisallowMultipleComponent]
    public class DualGridTileProxy : MonoBehaviour
    {
        [HideInInspector] public DualGrid3D owner;
        [HideInInspector] public LandscapeCellKey cellKey;
        [HideInInspector] public bool notifyOwnerOnDestroy = true;

        void OnDestroy()
        {
            if (!notifyOwnerOnDestroy || owner == null)
                return;

            owner.HandleDisplayTileDestroyed(cellKey);
        }
    }
}
