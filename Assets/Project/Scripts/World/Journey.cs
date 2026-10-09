using System;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.World
{
    [DisallowMultipleComponent]
    public class Journey : MonoBehaviour
    {
        [SerializeField] Transform viewer;
        [SerializeField] WorldTerrainStreamer world;
        [SerializeField] Behaviour[] suspendWhileTravelling;
        [SerializeField] float walkMetresPerSecond = 4f;
        [SerializeField] float eyeHeight = 1.8f;
        [SerializeField, Min(0.1f)] float backOffPerSecond = 1.2f;
        [SerializeField, Min(0.01f)] float recoverPerSecond = 0.25f;
        [SerializeField, Min(1f)] float slowestMultiple = 8f;

        // How far ahead the streamer is told to build. At three hundred times walking pace the ring
        // cannot keep up behind the player, so it is pointed where they are about to be instead
        const float LeadSeconds = 8f;
        const float LeadMetres = 4000f;

        public event Action<string> Progress;
        public event Action Arrived;

        // Arrived fires on stopping too, this tells reaching the end from giving up on the way
        public bool Finished => total > 0f && travelled >= total;

        public bool Travelling { get; private set; }

        // Standing still because the ground ahead has not been built yet, rather than because the
        // journey is over. Worth showing, at three hundred times walking pace this is most of it
        public bool Waiting { get; private set; }

        // What travel settled at, against what was asked for. Both in metres per second
        public float Pace => speed;
        public float Ceiling => ceiling;

        public Vector2[] Path => path;

        Vector2[] path;
        float speed;
        float ceiling;
        float slowest;
        CharacterController controller;
        float hours;
        float travelled;
        float total;
        float steering;
        int steerSide = 1;

        public void Begin(TravelPlan plan, float speedMultiplier)
        {
            if (viewer == null || plan.Path == null || plan.Path.Length < 2)
                return;

            path = plan.Path;
            hours = plan.Hours;
            ceiling = walkMetresPerSecond * Mathf.Max(1f, speedMultiplier);
            slowest = Mathf.Min(ceiling, walkMetresPerSecond * slowestMultiple);
            speed = ceiling;
            controller = viewer.GetComponent<CharacterController>();
            travelled = 0f;
            total = 0f;

            for (int i = 0; i < path.Length - 1; i++)
                total += Vector2.Distance(path[i], path[i + 1]) * WorldSurface.MetresPerKm;

            if (total <= 0f)
                return;

            Suspend(true);
            Place(path[0]);
            Travelling = true;
        }

        // Stopping is always allowed and always leaves you exactly where you stood, with the clock
        // advanced by however much of the journey you actually made
        public void Stop()
        {
            if (!Travelling)
                return;

            Travelling = false;
            Waiting = false;
            Suspend(false);

            if (world != null)
                world.LeadTowards(null);

            Arrived?.Invoke();
        }

        void Update()
        {
            if (!Travelling)
                return;

            if (world != null)
            {
                Vector2 soon = Ahead(travelled + Mathf.Min(speed * LeadSeconds, LeadMetres));
                world.LeadTowards(new Vector3(soon.x * WorldSurface.MetresPerKm, 0f, soon.y * WorldSurface.MetresPerKm));
            }

            // The streamer lays about six tiles a second and travelling covers far more ground than
            // that, so a journey that walked on regardless spent its time over country that had not
            // arrived, which is what the hitching was. Waiting costs nothing, the clock is advanced
            // by distance covered, so a frame that covers none advances none
            Waiting = !Ahead();
            Settle(Waiting);

            if (Waiting)
            {
                Progress?.Invoke("waiting for the ground ahead");
                return;
            }

            float before = travelled;
            Walk();

            // Distance actually covered, not distance intended. Walking into a rock face now costs
            // you the time you spent walking into it, and the clock follows the ground rather than
            // the plan
            AdvanceClock(before, travelled);

            if (travelled >= total)
            {
                Stop();
                return;
            }

            Progress?.Invoke($"{(total - travelled) / WorldSurface.MetresPerKm:0} km to go");
        }

        // Real seconds, not scaled: this is about how fast the machine can build ground, which has
        // nothing to do with the game clock
        void Settle(bool waiting)
        {
            speed = waiting
                ? Mathf.Max(slowest, speed * (1f - backOffPerSecond * Time.unscaledDeltaTime))
                : Mathf.MoveTowards(speed, ceiling, ceiling * recoverPerSecond * Time.unscaledDeltaTime);
        }

        // The clock is driven by distance covered, not by real seconds, so the playback speed changes
        // how long you sit there and nothing else about the journey
        void AdvanceClock(float before, float now)
        {
            float share = (now - before) / total;
            WorldClock.Instance?.Skip(share * hours * WorldClock.MinutesPerHour);
        }

        // Driven through the CharacterController so the world still stops you. Teleporting along the
        // route was simpler and meant walking through trees, walls and townsfolk
        void Walk()
        {
            Vector2 aimKm = Ahead(travelled + speed * Time.deltaTime);
            Vector3 aim = WorldOrigin.UnityAt(aimKm, viewer.position.y);

            Vector3 step = aim - viewer.position;
            step.y = 0f;

            if (steering > 0f)
            {
                steering -= Time.deltaTime;
                step = Quaternion.Euler(0f, 55f * steerSide, 0f) * step;
            }

            Vector3 was = viewer.position;

            if (controller != null && controller.enabled)
            {
                controller.Move(step + Vector3.down * 9.81f * Time.deltaTime);

                // Blocked. Take a few steps at an angle before trying the line again, and alternate
                // sides so a dead end does not become a corner to grind against
                if (controller.collisionFlags.HasFlag(CollisionFlags.Sides) && steering <= 0f)
                {
                    steering = 0.6f;
                    steerSide = -steerSide;
                }
            }
            else
            {
                viewer.position += step;
            }

            Ground();

            Vector3 moved = viewer.position - was;
            moved.y = 0f;
            travelled = Mathf.Min(total, travelled + moved.magnitude);

            Face(new Vector2(step.x, step.z));
        }

        // The ground the next moment of walking lands on. Only the near ring carries collision, so
        // this asks about the tile itself rather than about anything visible on the horizon
        bool Ahead()
        {
            if (world == null || !world.Ready)
                return false;

            Vector2 km = Ahead(travelled + speed * Time.deltaTime);

            return world.HasGroundAt(WorldOrigin.UnityAt(km, viewer.position.y));
        }

        Vector2 Ahead(float distance)
        {
            float along = 0f;

            for (int i = 0; i < path.Length - 1; i++)
            {
                float span = Vector2.Distance(path[i], path[i + 1]) * WorldSurface.MetresPerKm;

                if (distance <= along + span || i == path.Length - 2)
                {
                    return Vector2.Lerp(path[i], path[i + 1], span <= 0f ? 0f : (distance - along) / span);
                }

                along += span;
            }

            return path[path.Length - 1];
        }

        // The ring cannot keep up at travelling speed, so this catches the case where there is no
        // terrain under the player yet. It only ever lifts them, it never overrides a real collision
        void Ground()
        {
            if (world == null || world.Surface == null)
                return;

            Vector2 km = WorldOrigin.KmAt(viewer.position);
            float floor = world.Surface.Height(km) + eyeHeight * 0.5f;

            if (viewer.position.y >= floor)
                return;

            if (controller != null) controller.enabled = false;
            viewer.position = new Vector3(viewer.position.x, floor, viewer.position.z);
            if (controller != null) controller.enabled = true;
        }

        // Height comes from the surface rather than from whatever terrain happens to have streamed
        // in. At travelling speed the ring cannot keep up, and standing on a tile that has not
        // arrived yet means falling through the world
        void Place(Vector2 km)
        {
            if (world == null || world.Surface == null)
                return;

            if (controller == null)
                controller = viewer.GetComponent<CharacterController>();

            if (controller != null)
                controller.enabled = false;

            viewer.position = WorldOrigin.UnityAt(km, world.Surface.Height(km) + eyeHeight);

            if (controller != null)
                controller.enabled = true;
        }

        void Face(Vector2 heading)
        {
            if (heading.sqrMagnitude < 1e-6f)
                return;

            viewer.rotation = Quaternion.LookRotation(
                new Vector3(heading.x, 0f, heading.y).normalized, Vector3.up);
        }

        void Suspend(bool travelling)
        {
            if (suspendWhileTravelling == null)
                return;

            foreach (Behaviour behaviour in suspendWhileTravelling)
                if (behaviour != null) behaviour.enabled = !travelling;
        }
    }
}
