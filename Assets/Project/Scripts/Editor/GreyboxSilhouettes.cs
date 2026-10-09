using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.EditorTools
{
    public static class GreyboxSilhouettes
    {
        public enum Build { Upright, Beast, Hunched, Robed, Winged, Swarm, Blob, Hulk }

        public static Build BuildOf(string enemyAsset)
        {
            switch (enemyAsset)
            {
                case "Enemy_Rat":
                case "Enemy_Warhound": return Build.Beast;
                case "Enemy_Spider": return Build.Swarm;
                case "Enemy_Bat": return Build.Winged;
                case "Enemy_Slime": return Build.Blob;
                case "Enemy_Zombie":
                case "Enemy_Ghoul": return Build.Hunched;
                case "Enemy_Cultist":
                case "Enemy_Archmage":
                case "Enemy_Wraith": return Build.Robed;
                case "Enemy_Imp": return Build.Winged;
                case "Enemy_BoneLord":
                case "Enemy_Warlord": return Build.Hulk;
                default: return Build.Upright;
            }
        }

        public static void Shape(Transform root, Transform body, Build build, float height, float radius,
            Material skin, Material trim)
        {
            switch (build)
            {
                case Build.Beast:
                    Lie(body, height, radius);
                    Part(root, "Snout", new Vector3(0f, height * 0.45f, radius * 2.1f), Quaternion.identity,
                        new Vector3(radius * 0.8f, radius * 0.7f, radius * 1.4f), skin);
                    Part(root, "Tail", new Vector3(0f, height * 0.5f, -radius * 2.4f), Quaternion.Euler(-24f, 0f, 0f),
                        new Vector3(radius * 0.3f, radius * 0.3f, radius * 2.6f), skin);
                    Legs(root, height * 0.28f, radius, skin);
                    break;

                case Build.Swarm:
                    Lie(body, height * 0.7f, radius);
                    Legs(root, height * 0.34f, radius * 1.9f, skin, 8, radius * 1.7f);
                    break;

                case Build.Winged:
                    Part(root, "WingLeft", new Vector3(-radius * 2.2f, height * 0.72f, 0f), Quaternion.Euler(0f, 0f, 22f),
                        new Vector3(radius * 3.4f, 0.06f, radius * 1.8f), trim);
                    Part(root, "WingRight", new Vector3(radius * 2.2f, height * 0.72f, 0f), Quaternion.Euler(0f, 0f, -22f),
                        new Vector3(radius * 3.4f, 0.06f, radius * 1.8f), trim);
                    Part(root, "Horns", new Vector3(0f, height * 1.02f, 0f), Quaternion.Euler(0f, 0f, 18f),
                        new Vector3(radius * 1.5f, radius * 0.5f, radius * 0.3f), trim);
                    break;

                case Build.Hunched:
                    body.localRotation = Quaternion.Euler(24f, 0f, 0f);
                    body.localPosition = new Vector3(0f, height * 0.44f, -radius * 0.3f);
                    Part(root, "ArmLeft", new Vector3(-radius * 1.3f, height * 0.5f, radius * 0.9f),
                        Quaternion.Euler(64f, 0f, 0f), new Vector3(radius * 0.4f, radius * 0.4f, height * 0.5f), skin);
                    Part(root, "ArmRight", new Vector3(radius * 1.3f, height * 0.5f, radius * 0.9f),
                        Quaternion.Euler(64f, 0f, 0f), new Vector3(radius * 0.4f, radius * 0.4f, height * 0.5f), skin);
                    break;

                case Build.Robed:
                    // A cone, which nothing else in the bestiary is: robes read at any distance.
                    body.localScale = new Vector3(radius * 1.2f, height * 0.5f, radius * 1.2f);
                    Part(root, "Skirt", new Vector3(0f, height * 0.22f, 0f), Quaternion.identity,
                        new Vector3(radius * 3.2f, height * 0.44f, radius * 3.2f), skin);
                    Part(root, "Hood", new Vector3(0f, height * 0.95f, 0f), Quaternion.identity,
                        new Vector3(radius * 1.7f, radius * 1.3f, radius * 1.7f), trim);
                    Part(root, "Staff", new Vector3(radius * 1.6f, height * 0.6f, 0f), Quaternion.identity,
                        new Vector3(0.07f, height * 0.62f, 0.07f), trim);
                    break;

                case Build.Blob:
                    body.localScale = new Vector3(radius * 2.6f, height * 0.34f, radius * 2.6f);
                    body.localPosition = new Vector3(0f, height * 0.3f, 0f);
                    break;

                case Build.Hulk:
                    body.localScale = new Vector3(radius * 2.6f, height * 0.5f, radius * 2.2f);
                    Part(root, "Pauldrons", new Vector3(0f, height * 0.78f, 0f), Quaternion.identity,
                        new Vector3(radius * 3.4f, radius * 0.6f, radius * 1.6f), trim);
                    Part(root, "Crest", new Vector3(0f, height * 1.04f, 0f), Quaternion.identity,
                        new Vector3(radius * 0.4f, radius * 1.1f, radius * 1.4f), trim);
                    Legs(root, height * 0.42f, radius * 1.1f, skin);
                    break;

                default:
                    Part(root, "Head", new Vector3(0f, height * 0.94f, 0f), Quaternion.identity,
                        new Vector3(radius * 1.3f, radius * 1.2f, radius * 1.3f), trim);
                    Legs(root, height * 0.46f, radius, skin);
                    break;
            }
        }

        static void Lie(Transform body, float height, float radius)
        {
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.localPosition = new Vector3(0f, height * 0.55f, 0f);
            body.localScale = new Vector3(radius * 1.8f, radius * 2.2f, radius * 1.8f);
        }

        static void Legs(Transform root, float length, float radius, Material skin, int count = 4, float spread = 0f)
        {
            float reach = spread > 0f ? spread : radius * 0.8f;

            for (int i = 0; i < count; i++)
            {
                float angle = 360f / count * i + 45f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * reach;

                Part(root, $"Leg{i}", new Vector3(offset.x, length * 0.5f, offset.z),
                    Quaternion.identity, new Vector3(0.1f, length, 0.1f), skin);
            }
        }

        // What a townsperson is holding, which is the only thing that tells a smith from a priest
        // when both are the same box in the same street
        public static void Token(Transform root, RoomPurpose trade, Material material)
        {
            switch (trade)
            {
                case RoomPurpose.Smithy:
                    Part(root, "Hammer", new Vector3(0.42f, 1.05f, 0.2f), Quaternion.Euler(0f, 0f, 20f),
                        new Vector3(0.08f, 0.6f, 0.08f), material);
                    Part(root, "Head", new Vector3(0.42f, 1.38f, 0.2f), Quaternion.identity,
                        new Vector3(0.26f, 0.16f, 0.16f), material);
                    break;

                case RoomPurpose.Armorer:
                    Part(root, "Shield", new Vector3(-0.42f, 1.0f, 0.18f), Quaternion.Euler(0f, 0f, 8f),
                        new Vector3(0.5f, 0.62f, 0.09f), material);
                    break;

                case RoomPurpose.Alchemist:
                    Part(root, "Bottle", new Vector3(0.38f, 1.06f, 0.2f), Quaternion.identity,
                        new Vector3(0.16f, 0.3f, 0.16f), material);
                    Part(root, "Stopper", new Vector3(0.38f, 1.26f, 0.2f), Quaternion.identity,
                        new Vector3(0.07f, 0.12f, 0.07f), material);
                    break;

                case RoomPurpose.Temple:
                    Part(root, "Stole", new Vector3(0f, 1.15f, 0.34f), Quaternion.identity,
                        new Vector3(0.22f, 0.8f, 0.05f), material);
                    break;

                case RoomPurpose.Watchhouse:
                    Part(root, "Spear", new Vector3(0.4f, 1.2f, 0.16f), Quaternion.identity,
                        new Vector3(0.07f, 1.5f, 0.07f), material);
                    Part(root, "Helm", new Vector3(0f, 1.92f, 0f), Quaternion.identity,
                        new Vector3(0.36f, 0.2f, 0.36f), material);
                    break;

                case RoomPurpose.Inn:
                case RoomPurpose.Tavern:
                    Part(root, "Tray", new Vector3(0.36f, 1.1f, 0.26f), Quaternion.identity,
                        new Vector3(0.42f, 0.05f, 0.34f), material);
                    break;

                case RoomPurpose.Market:
                case RoomPurpose.GeneralStore:
                    Part(root, "Pack", new Vector3(0f, 1.2f, -0.34f), Quaternion.identity,
                        new Vector3(0.48f, 0.5f, 0.3f), material);
                    break;

                default:
                    Part(root, "Satchel", new Vector3(0.3f, 0.92f, -0.2f), Quaternion.identity,
                        new Vector3(0.3f, 0.26f, 0.16f), material);
                    break;
            }
        }

        // One shape per WeaponClass, in the enum's order, held in the viewmodel's weapon root.
        // Built along +Y like the sword it replaces, because the viewmodel's poses were authored
        // against that and changing the axis would swing every weapon sideways
        public static void WeaponShape(Transform root, WeaponClass weapon, Material metal, Material grip)
        {
            switch (weapon)
            {
                case WeaponClass.Thrust:
                    Part(root, "Blade", new Vector3(0f, 0.22f, 0f), Quaternion.identity,
                        new Vector3(0.035f, 0.38f, 0.012f), metal);
                    Part(root, "Grip", new Vector3(0f, -0.06f, 0f), Quaternion.identity,
                        new Vector3(0.04f, 0.14f, 0.04f), grip);
                    break;

                case WeaponClass.Axe:
                    Part(root, "Haft", new Vector3(0f, 0.3f, 0f), Quaternion.identity,
                        new Vector3(0.05f, 0.9f, 0.05f), grip);
                    Part(root, "Head", new Vector3(0.11f, 0.66f, 0f), Quaternion.identity,
                        new Vector3(0.26f, 0.3f, 0.05f), metal);
                    Part(root, "Beard", new Vector3(0.07f, 0.5f, 0f), Quaternion.identity,
                        new Vector3(0.14f, 0.16f, 0.05f), metal);
                    break;

                case WeaponClass.Blunt:
                    Part(root, "Haft", new Vector3(0f, 0.24f, 0f), Quaternion.identity,
                        new Vector3(0.05f, 0.66f, 0.05f), grip);
                    Part(root, "Head", new Vector3(0f, 0.6f, 0f), Quaternion.identity,
                        new Vector3(0.18f, 0.22f, 0.18f), metal);
                    break;

                case WeaponClass.Bow:
                    Part(root, "Limb", new Vector3(0f, 0.24f, 0f), Quaternion.identity,
                        new Vector3(0.04f, 1.05f, 0.04f), grip);
                    Part(root, "String", new Vector3(0f, 0.24f, -0.09f), Quaternion.identity,
                        new Vector3(0.012f, 0.98f, 0.012f), metal);
                    break;

                case WeaponClass.Unarmed:
                    Part(root, "Fist", new Vector3(0f, 0.04f, 0f), Quaternion.identity,
                        new Vector3(0.13f, 0.15f, 0.13f), grip);
                    break;

                default:
                    Part(root, "Blade", new Vector3(0f, 0.46f, 0f), Quaternion.identity,
                        new Vector3(0.05f, 0.86f, 0.015f), metal);
                    Part(root, "Guard", new Vector3(0f, 0.04f, 0f), Quaternion.identity,
                        new Vector3(0.24f, 0.045f, 0.045f), metal);
                    Part(root, "Grip", new Vector3(0f, -0.11f, 0f), Quaternion.identity,
                        new Vector3(0.045f, 0.24f, 0.045f), grip);
                    break;
            }
        }

        static void Part(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale,
            Material material) =>
            GreyboxContentBuilder.AddVisual(parent, name, position, rotation, scale, material);
    }
}
