using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarrowBuildHook
{
    public class REPARENT_PASS : MonoBehaviour, IBuildPass
    {
        public int PassPriority => Priority;
        [Tooltip("Lowest Priority Pass runs first.")]
        public int Priority;

        bool IBuildPass.PassWhenInactive => RunWhenDisabled;
        public bool RunWhenDisabled;

        public HumanBodyBones Target = HumanBodyBones.Head;

        void IBuildPass.OnBuild()
        {
            Transform myT = transform;
            DestroyImmediate(this);

            var targT = myT.GetComponentInParent<SLZ.VRMK.Avatar>()?.animator?.GetBoneTransform(Target);
            if (targT != null)
                myT.SetParent(targT, true);
        }
    }
}
