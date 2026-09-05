using System;
using UnityEngine;

namespace AglenRealms.WorldCore
{
    [Serializable]
    public class EnvironmentBrushSettings
    {
        public bool randomRotation;
        public bool randomScale;
        public Vector2 randomScaleRange = new Vector2(0.9f, 1.1f);
        public bool alignToSurface;
        public EnvironmentAlignMode alignMode = EnvironmentAlignMode.Landscape;
        public LayerMask alignLayerMask = ~0;
    }
}
