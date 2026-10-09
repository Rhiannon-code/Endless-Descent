using EndlessDescent.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace EndlessDescent.World
{
    // Underground has to be its own place to stand, not just the outdoors with the sun switched off
    // Hiding the terrain left the sky behind it, and culled rooms are holes you see daylight through,
    // so indoors gets a black sky and fog that closes in at about twenty metres
    [DisallowMultipleComponent]
    public class UndergroundAir : MonoBehaviour
    {
        const float SeeingDistanceMetres = 22f;

        Volume volume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Attach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            if (FindFirstObjectByType<UndergroundAir>() != null)
                return;

            new GameObject("Underground Air").AddComponent<UndergroundAir>();
        }

        void Awake()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // No sky at all, whatever the dungeon does not cover reads as black rather than as noon
            VisualEnvironment environment = profile.Add<VisualEnvironment>();
            environment.skyType.overrideState = true;
            environment.skyType.value = 0;
            environment.skyAmbientMode.overrideState = true;
            environment.skyAmbientMode.value = SkyAmbientMode.Static;

            Fog fog = profile.Add<Fog>();
            fog.enabled.overrideState = true;
            fog.enabled.value = true;
            fog.colorMode.overrideState = true;
            fog.colorMode.value = FogColorMode.ConstantColor;
            fog.color.overrideState = true;
            fog.color.value = new Color(0.02f, 0.02f, 0.025f);
            fog.meanFreePath.overrideState = true;
            fog.meanFreePath.value = SeeingDistanceMetres;
            fog.maxFogDistance.overrideState = true;
            fog.maxFogDistance.value = SeeingDistanceMetres * 6f;

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            // Above the exposure volume the scene builder writes, which sits at 1
            volume.priority = 10f;
            volume.sharedProfile = profile;
            volume.weight = 0f;
        }

        void OnEnable()
        {
            CurrentSpace.Changed += OnSpaceChanged;
            OnSpaceChanged(CurrentSpace.Kind);
        }

        void OnDisable() => CurrentSpace.Changed -= OnSpaceChanged;

        void OnSpaceChanged(SpaceKind kind) => volume.weight = kind == SpaceKind.Overworld ? 0f : 1f;
    }
}
