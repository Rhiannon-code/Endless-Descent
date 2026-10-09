using EndlessDescent.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndlessDescent.World
{
    // Keeps the player within a couple of kilometres of Unity's origin by moving the whole scene back
    // under them in whole kilometres. Nothing in the game uses a NavMesh or saves a position,
    // which is what makes moving everything at once safe
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public class FloatingOrigin : MonoBehaviour
    {
        [SerializeField] Transform viewer;
        [SerializeField, Min(500f)] float recentreMetres = 2000f;

        public void Bind(Transform target) => viewer = target;

        void Update()
        {
            if (viewer == null)
                return;

            Vector3 at = viewer.position;

            if (Mathf.Abs(at.x) > recentreMetres || Mathf.Abs(at.z) > recentreMetres)
                Recentre();
        }

        public void Recentre()
        {
            if (viewer == null)
                return;

            Vector3 at = viewer.position;
            Vector3 by = new Vector3(
                Mathf.Round(at.x / WorldSurface.MetresPerKm) * WorldSurface.MetresPerKm, 0f,
                Mathf.Round(at.z / WorldSurface.MetresPerKm) * WorldSurface.MetresPerKm);

            if (by == Vector3.zero)
                return;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    // A screen space canvas ignores its transform, and moving one only confuses the layout
                    if (root.GetComponent<Canvas>() != null)
                        continue;

                    root.transform.position -= by;
                }
            }

            // Colliders, the character controller among them, only see a moved transform once synced
            Physics.SyncTransforms();
            WorldOrigin.Recentre(by);
        }
    }
}
