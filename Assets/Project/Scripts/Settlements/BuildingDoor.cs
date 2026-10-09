using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Dungeons;
using UnityEngine;

namespace EndlessDescent.Settlements
{
    // The front door of a building, and the one inside it that leads back out. The interior is not
    // built until somebody actually opens the door, a city has a hundred of these and nobody is
    // ever in more than one of them
    [DisallowMultipleComponent]
    public class BuildingDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] string label = "building";
        [SerializeField] Transform destination;
        [SerializeField] RoomModuleDefinition interior;
        [SerializeField] GameObject doorPlug;
        [SerializeField] GameObject resident;
        [SerializeField] GameObject service;
        [SerializeField] Transform interiorHost;
        [SerializeField] Transform outside;
        [SerializeField] Vector3 interiorOrigin;
        [SerializeField] bool leadsOut;

        public string Prompt => destination != null || interior != null ? $"Enter the {label}" : $"The {label} is shut";

        public bool CanInteract(GameObject actor) => destination != null || interior != null;

        // The interior's spot is kept as an offset from the host, not as a position, the whole scene
        // moves when the origin is recentred
        public void Configure(string name, RoomModuleDefinition module, GameObject plug, GameObject occupant,
            GameObject fitting, Transform host, Vector3 origin, Transform street)
        {
            label = name;
            interior = module;
            doorPlug = plug;
            resident = occupant;
            service = fitting;
            interiorHost = host;
            interiorOrigin = origin;
            outside = street;
        }

        public void LeadsTo(Transform exit)
        {
            label = "street";
            destination = exit;
            leadsOut = true;
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor))
                return;

            // Behind a fade, building the interior and moving the player both happen in the dark
            Transition.Start(string.Empty, Transition.Quick, null, () => Step(actor));
        }

        void Step(GameObject actor)
        {
            if (destination == null)
                destination = BuildInterior();

            if (destination == null)
                return;

            Move(actor, destination);
            CurrentSpace.Enter(leadsOut ? SpaceKind.Overworld : SpaceKind.Interior);
        }

        // Moving a CharacterController by assigning its transform is ignored while it is enabled,
        // it writes the old position back on the next internal move
        static void Move(GameObject actor, Transform to)
        {
            CharacterController controller = actor.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;

            if (wasEnabled)
                controller.enabled = false;

            actor.transform.SetPositionAndRotation(to.position, to.rotation);

            if (wasEnabled)
                controller.enabled = true;

            Physics.SyncTransforms();
        }

        Transform BuildInterior()
        {
            if (interior == null || interior.Prefab == null)
                return null;

            GameObject room = Instantiate(interior.Prefab, interiorHost);
            room.transform.SetLocalPositionAndRotation(interiorOrigin, Quaternion.identity);
            room.name = $"Interior_{label}";

            RoomModule module = room.GetComponent<RoomModule>();
            if (module == null || module.Connectors == null || module.Connectors.Count == 0)
                return room.transform;

            // Every doorway is bricked up except the one you came in by, so an interior is a room
            // rather than a hole opening onto empty space
            ModuleConnector way = module.Connectors[0];

            foreach (ModuleConnector connector in module.Connectors)
            {
                if (connector == null || doorPlug == null)
                    continue;

                Instantiate(doorPlug, connector.transform.position, connector.transform.rotation, room.transform);
            }

            GameObject exit = new GameObject("ExitDoor");
            exit.transform.SetParent(room.transform, false);
            exit.transform.SetPositionAndRotation(
                way.transform.position - way.transform.forward * 1.2f,
                Quaternion.LookRotation(-way.transform.forward, Vector3.up));

            BoxCollider trigger = exit.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.4f, 2.8f, 0.6f);
            trigger.center = new Vector3(0f, 1.4f, 0f);

            exit.AddComponent<BuildingDoor>().LeadsTo(outside != null ? outside : transform);

            GameObject landing = new GameObject("Landing");
            landing.transform.SetParent(room.transform, false);
            landing.transform.SetPositionAndRotation(
                way.transform.position - way.transform.forward * 2.6f,
                Quaternion.LookRotation(-way.transform.forward, Vector3.up));

            if (resident != null)
                Instantiate(resident, room.transform.position, Quaternion.identity, room.transform);

            // Off to one side of the room, so it is not standing on whoever lives here
            if (service != null)
            {
                GameObject fitting = Instantiate(service, room.transform);
                fitting.transform.SetLocalPositionAndRotation(new Vector3(2.2f, 0f, 2.2f), Quaternion.identity);
            }

            return landing.transform;
        }
    }
}
