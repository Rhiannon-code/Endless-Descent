using System.Collections;
using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.World
{
    [DisallowMultipleComponent]
    public class ShaderWarmup : MonoBehaviour
    {
        [SerializeField] ShaderVariantCollection collection;
        [SerializeField, Min(1)] int variantsPerStep = 8;

        public bool HasCollection => collection != null;
        public int VariantCount => collection != null ? collection.variantCount : 0;

        public IEnumerator Run()
        {
            if (collection == null)
                yield break;

            // Returns false once there is nothing left, so the loop ends by itself rather than on a
            // count that could disagree with what the collection actually holds
            while (collection.WarmUpProgressively(variantsPerStep))
                yield return Steps.WaitFrame;
        }
    }
}
