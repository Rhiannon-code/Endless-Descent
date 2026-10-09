using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Dungeons/Module Set", fileName = "ModuleSet")]
    public class ModuleSet : ScriptableObject
    {
        [SerializeField] RoomModuleDefinition[] modules;

        public IReadOnlyList<RoomModuleDefinition> Modules => modules;

        public void CollectFor(RoomRole role, List<RoomModuleDefinition> into)
        {
            into.Clear();

            if (modules == null)
                return;

            foreach (RoomModuleDefinition module in modules)
            {
                if (module != null && module.Prefab != null && module.CanServe(role))
                    into.Add(module);
            }
        }

        public bool CoversEveryRole(out RoomRole missing)
        {
            foreach (RoomRole role in System.Enum.GetValues(typeof(RoomRole)))
            {
                bool covered = false;

                if (modules != null)
                {
                    foreach (RoomModuleDefinition module in modules)
                    {
                        if (module != null && module.Prefab != null && module.CanServe(role))
                        {
                            covered = true;
                            break;
                        }
                    }
                }

                if (!covered)
                {
                    missing = role;
                    return false;
                }
            }

            missing = default;
            return true;
        }
    }
}
