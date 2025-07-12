using System.Collections;
using System.Collections.Generic;
using UltEvents;
using UnityEngine;

namespace MarrowBuildHook
{
    [RequireComponent(typeof(UltEventHolder))]
    public class MODIFY_PASS : MonoBehaviour, IBuildPass
    {
        public int PassPriority => Priority;
        [Tooltip("Lowest Priority Pass runs first.")]
        public int Priority;

        bool IBuildPass.PassWhenInactive => RunWhenDisabled;
        public bool RunWhenDisabled;

        void IBuildPass.OnBuild()
        {
            var ult = GetComponent<UltEventHolder>();
            if (!ReferenceEquals(ult, null))
            {
                ult.Invoke();
                DestroyImmediate(this);
                DestroyImmediate(ult);
            }
        }
    }
}
