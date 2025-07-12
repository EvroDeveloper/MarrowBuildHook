using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarrowBuildHook
{
    public class EDITOR_ONLY : MonoBehaviour, IBuildPass
    {
        public int PassPriority => Priority;
        [Tooltip("Lowest Priority Pass runs first.")]
        public int Priority;

        bool IBuildPass.PassWhenInactive => RunWhenDisabled;
        public bool RunWhenDisabled;

        void IBuildPass.OnBuild() => DestroyImmediate(gameObject);
        
    }
}
