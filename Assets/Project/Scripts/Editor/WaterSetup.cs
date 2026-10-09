using EndlessDescent.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EndlessDescent.EditorTools
{
    // Fills in what the water needs in the scene that is already open. It never generates a scene and
    // never touches one that is not open, so there is nothing here a rebuild can wipe
    // The river material is made in code rather than by hand because a transparent HDRP material is a
    // surface type, a render queue and a set of shader keywords that have to agree with each other,
    // and HDMaterial.SetSurfaceType is the only thing that gets all three right
    public static class WaterSetup
    {
        const string MaterialPath = "Assets/Project/Content/Greybox/Materials/Mat_River.mat";

        [MenuItem("Endless Descent/Water/Set Up Water In Open Scene")]
        public static void SetUp()
        {
            Material river = River();

            WorldTerrainStreamer streamer = Object.FindFirstObjectByType<WorldTerrainStreamer>();

            if (streamer == null)
            {
                Debug.LogError("No WorldTerrainStreamer in the open scene, so there is no ground for " +
                               "water to sit in. Open Scenes/World/Ordovan.unity and run this again.");
                return;
            }

            WorldWater water = Object.FindFirstObjectByType<WorldWater>();

            if (water == null)
            {
                GameObject go = new GameObject("Water");
                Undo.RegisterCreatedObjectUndo(go, "Add water");
                water = go.AddComponent<WorldWater>();
            }

            SerializedObject onWater = new SerializedObject(water);
            onWater.FindProperty("riverMaterial").objectReferenceValue = river;
            onWater.ApplyModifiedProperties();

            SerializedObject onStreamer = new SerializedObject(streamer);
            onStreamer.FindProperty("water").objectReferenceValue = water;
            onStreamer.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(streamer.gameObject.scene);

            Debug.Log($"Water set up: {river.name} on {water.name}, and {water.name} on " +
                      $"{streamer.name}. Save the scene to keep it.");
        }

        // Public because PlaytestSceneBuilder needs the same material and a second copy of these
        // settings would drift from this one
        public static Material River()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (existing != null)
                return existing;

            Material material = new Material(Shader.Find("HDRP/Lit")) { name = "Mat_River" };

            // Running water is the one surface here that is not an HDRP water surface, so everything
            // that makes it read as water rather than as glass is set explicitly
            HDMaterial.SetSurfaceType(material, true);
            HDMaterial.SetRenderingPass(material, HDMaterial.RenderingPass.Default);

            material.SetColor("_BaseColor", new Color(0.17f, 0.31f, 0.34f, 0.62f));
            material.SetFloat("_Smoothness", 0.94f);
            material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;

            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();

            return material;
        }
    }
}
