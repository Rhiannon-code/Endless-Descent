using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Combat
{
    [DisallowMultipleComponent]
    public class AiActivation : MonoBehaviour
    {
        [SerializeField] Transform viewer;
        [SerializeField, Min(1f)] float thinkingRange = 45f;
        [SerializeField, Min(0.05f)] float interval = 0.25f;

        float next;

        public int Thinking { get; private set; }

        void Update()
        {
            if (viewer == null || Time.time < next)
                return;

            next = Time.time + interval;

            // Nothing thinks while the screen is dark, the player cannot answer it
            bool dark = EndlessDescent.Core.Transition.Busy;
            float range = thinkingRange * thinkingRange;
            Vector3 here = viewer.position;

            Thinking = 0;

            IReadOnlyList<EnemyBrain> brains = EnemyBrain.Alive;

            for (int i = 0; i < brains.Count; i++)
            {
                EnemyBrain brain = brains[i];
                if (brain == null)
                    continue;

                bool near = !dark && (brain.transform.position - here).sqrMagnitude <= range;

                if (near)
                    Thinking++;

                brain.SetThinking(near);
            }

            // The lit ness test reads this, and which lights are on changes as rooms are culled. With
            // nothing close enough to think there is nobody to read it
            if (Thinking > 0)
                EnemySenses.RefreshLights();
        }
    }
}
