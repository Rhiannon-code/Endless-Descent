using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Combat
{
    // Sight and hearing, replacing the flat distance check the brain used to do. Sight needs the
    // target in front, in range and not behind geometry, hearing only needs them to be loud enough
    [DisallowMultipleComponent]
    public class EnemySenses : MonoBehaviour
    {
        [SerializeField] EnemyDefinition definition;
        [SerializeField] Transform eyes;
        [SerializeField, Range(20f, 360f)] float fieldOfView = 120f;
        [SerializeField] float loseTargetAfter = 6f;
        [SerializeField] LayerMask sightBlockers = ~0;
        [SerializeField] float darkRangeFloor = 0.35f;
        [SerializeField] float litDistance = 9f;

        Transform target;
        float awareUntil;

        public bool IsAware => Time.time < awareUntil;
        public Vector3 LastKnownPosition { get; private set; }
        public Transform Target => target;

        void Awake()
        {
            if (eyes == null)
                eyes = transform;
        }

        public void Bind(Transform who) => target = who;

        void Update()
        {
            if (target == null)
                return;

            if (CanSee() || CanHear())
            {
                awareUntil = Time.time + loseTargetAfter;
                LastKnownPosition = target.position;
            }
        }

        bool CanSee()
        {
            float range = definition != null ? definition.AggroRange : 12f;

            if (target.TryGetComponent(out IStealthProfile profile))
                range *= profile.VisibilityScale;

            // Torches are everywhere, so standing in the dark should be worth something
            range *= Mathf.Lerp(darkRangeFloor, 1f, Lit(target.position));

            Vector3 toTarget = target.position + Vector3.up - eyes.position;

            if (toTarget.magnitude > range)
                return false;

            if (Vector3.Angle(eyes.forward, toTarget) > fieldOfView * 0.5f)
                return false;

            // A wall between us is the whole point of sneaking
            return !Physics.Raycast(eyes.position, toTarget.normalized, toTarget.magnitude - 0.4f,
                sightBlockers, QueryTriggerInteraction.Ignore);
        }

        // How lit a point is, judged by the nearest torch. Cheap on purpose, this runs per enemy
        // per frame, and a dungeon carries hundreds of lights
        float Lit(Vector3 position)
        {
            Light nearest = null;
            float best = float.MaxValue;

            foreach (Light light in lightCache)
            {
                if (light == null)
                    continue;

                float distance = Vector3.Distance(light.transform.position, position);
                if (distance < best)
                {
                    best = distance;
                    nearest = light;
                }
            }

            if (nearest == null)
                return 1f;

            return Mathf.Clamp01(1f - best / Mathf.Max(1f, litDistance));
        }

        static Light[] lightCache = new Light[0];
        static readonly List<Light> found = new List<Light>();

        // Only the lights that are actually on. Culling switches most of a dungeon's several hundred
        // torches off, and walking the whole list per enemy per frame is what made this expensive.
        // AiActivation refreshes it on its own cadence rather than each sense doing it
        public static void RefreshLights()
        {
            found.Clear();

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.isActiveAndEnabled && light.type != LightType.Directional) found.Add(light);

            if (lightCache.Length != found.Count)
                lightCache = new Light[found.Count];

            found.CopyTo(lightCache);
        }

        // Half a dungeon storey
        const float HearingStorey = 2.5f;

        bool CanHear()
        {
            if (!target.TryGetComponent(out IStealthProfile profile))
                return false;

            // On its own floor only. Hearing has no line of sight, so something a storey below heard
            // footsteps through the floor and walked into its ceiling trying to reach them
            if (Mathf.Abs(target.position.y - transform.position.y) > HearingStorey)
                return false;

            return Vector3.Distance(transform.position, target.position) <= profile.NoiseRadius;
        }
    }
}
