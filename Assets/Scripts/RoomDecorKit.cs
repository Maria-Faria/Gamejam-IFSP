using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuardiasDaLuz.MapSystem
{
    [Serializable]
    public class RoomDecorKit
    {
        public string kitName = "Laboratório";
        [Tooltip("Nomes dos FBX de Assets/Decor que ficam de pé encostados nas paredes. No editor, os slots vazios são preenchidos a partir destes nomes.")]
        public string[] standingPropNames = { };
        [Tooltip("Nomes dos FBX de Assets/Decor presos na parede (telas, painéis).")]
        public string[] wallMountedPropNames = { };
        [Tooltip("Nomes dos FBX de Assets/Decor deitados no chão, sem colisão (painéis de piso).")]
        public string[] floorPanelNames = { };

        public GameObject[] standingProps;
        public GameObject[] wallMountedProps;
        public GameObject[] floorPanels;

        [Range(0, 20)] public int minStandingProps = 10;
        [Range(0, 20)] public int maxStandingProps = 16;
        [Range(0, 6)] public int maxWallMountedProps = 3;
        [Range(0, 6)] public int maxFloorPanels = 2;

        private static readonly Dictionary<string, float> PropScales = new Dictionary<string, float>
        {
            { "barrel", 1.1f }, { "bench", 1.7f }, { "bin", 1.0f }, { "cone", 0.8f }, { "crate", 1.1f },
            { "door", 2.4f }, { "window", 1.6f }, { "gate", 2.2f }, { "gun", 1.2f }, { "lamp", 3.0f }, { "laser", 1.6f }, { "led_floor", 1.8f },
            { "led_wall", 1.6f }, { "park", 1.4f }, { "pc", 1.6f }, { "plant", 1.4f }, { "server", 2.1f },
            { "sign", 1.2f }, { "sign_warn", 1.2f }, { "table", 1.6f }, { "toolbox", 1.0f }, { "trafic", 1.7f }
        };

        private const float WallMountHeight = 1.1f;

        private static readonly Dictionary<string, float> WallMountHeights = new Dictionary<string, float>
        {
            { "door", 0f }, { "window", 1.2f }
        };

        public void Decorate(Transform roomRoot, Vector3 center, float width, float length, float wallThickness,
            bool[] doorSides, float doorClearance)
        {
            Physics.SyncTransforms();
            Vector3[] inwards = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };

            int standingCount = HasAny(standingProps) ? UnityEngine.Random.Range(minStandingProps, maxStandingProps + 1) : 0;
            for (int placed = 0, attempt = 0; placed < standingCount && attempt < standingCount * 6; attempt++)
            {
                GameObject prefab = Pick(standingProps);
                int side = UnityEngine.Random.Range(0, 4);
                if (TryPlaceAgainstWall(prefab, roomRoot, center, width, length, wallThickness, side, inwards[side],
                        doorSides[side], doorClearance, 0f, true))
                {
                    placed++;
                }
            }

            int mountedCount = HasAny(wallMountedProps) ? UnityEngine.Random.Range(0, maxWallMountedProps + 1) : 0;
            for (int placed = 0, attempt = 0; placed < mountedCount && attempt < mountedCount * 6; attempt++)
            {
                GameObject prefab = Pick(wallMountedProps);
                int side = UnityEngine.Random.Range(0, 4);
                if (TryPlaceAgainstWall(prefab, roomRoot, center, width, length, wallThickness, side, inwards[side],
                        doorSides[side], doorClearance,
                        WallMountHeights.TryGetValue(prefab.name, out float height) ? height : WallMountHeight, false))
                {
                    placed++;
                }
            }

            int panelCount = HasAny(floorPanels) ? UnityEngine.Random.Range(0, maxFloorPanels + 1) : 0;
            for (int i = 0; i < panelCount; i++)
            {
                GameObject prefab = Pick(floorPanels);
                Vector3 position = center + new Vector3(UnityEngine.Random.Range(-width * 0.35f, width * 0.35f), 0.01f,
                    UnityEngine.Random.Range(-length * 0.35f, length * 0.35f));
                if (Mathf.Abs(position.x - center.x) < 3f && Mathf.Abs(position.z - center.z) < 3f) continue;
                SpawnProp(prefab, roomRoot, position, Quaternion.Euler(0f, UnityEngine.Random.Range(0, 4) * 90f, 0f), false);
            }
        }

        private bool TryPlaceAgainstWall(GameObject prefab, Transform roomRoot, Vector3 center, float width, float length,
            float wallThickness, int side, Vector3 inward, bool hasDoor, float doorClearance, float height, bool solid)
        {
            float scale = GetScale(prefab);
            Bounds local = GetLocalBounds(prefab);
            Vector3 size = local.size * scale;

            bool alongX = side < 2;
            float wallLength = alongX ? width : length;
            float halfSpan = wallLength * 0.5f - 2f - size.x * 0.5f;
            if (halfSpan <= 0f) return false;

            float along = UnityEngine.Random.Range(-halfSpan, halfSpan);
            if (hasDoor && Mathf.Abs(along) < doorClearance + size.x * 0.5f) return false;

            float halfDepth = (alongX ? length : width) * 0.5f;
            float offset = halfDepth - wallThickness * 0.5f - size.z * 0.5f - 0.15f;
            Vector3 position = center - inward * offset + (alongX ? Vector3.right : Vector3.forward) * along;
            position.y = height;

            Quaternion rotation = Quaternion.LookRotation(-inward, Vector3.up);
            if (solid)
            {
                Vector3 boxCenter = position + rotation * (local.center * scale);
                Vector3 halfExtents = size * 0.5f;
                boxCenter.y = Mathf.Max(boxCenter.y, halfExtents.y + 0.05f);
                if (Physics.CheckBox(boxCenter, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore)) return false;
            }

            SpawnProp(prefab, roomRoot, position, rotation, solid);
            return true;
        }

        public static GameObject SpawnProp(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, bool solid)
        {
            return SpawnWrapped(prefab, parent, position, rotation, Vector3.one * GetScale(prefab), solid, $"Decor_{prefab.name}");
        }

        public static GameObject SpawnWrapped(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation,
            Vector3 scale, bool solid, string name)
        {
            GameObject wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.SetPositionAndRotation(position, rotation);
            wrapper.transform.localScale = scale;
            GameObject model = UnityEngine.Object.Instantiate(prefab, wrapper.transform, false);
            foreach (Camera camera in model.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.Destroy(camera.gameObject);
            foreach (Light light in model.GetComponentsInChildren<Light>(true)) UnityEngine.Object.Destroy(light.gameObject);
            if (solid) AddBoxCollider(wrapper, GetLocalBounds(prefab));
            Physics.SyncTransforms();
            return wrapper;
        }

        public static void AddBoxCollider(GameObject obj, Bounds localBounds)
        {
            BoxCollider box = obj.AddComponent<BoxCollider>();
            box.center = localBounds.center;
            box.size = localBounds.size;
        }

        public static float GetScale(GameObject prefab)
        {
            return PropScales.TryGetValue(prefab.name, out float scale) ? scale : 1f;
        }

        public static Bounds GetLocalBounds(GameObject prefab)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool initialized = false;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                Bounds meshBounds = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return initialized ? bounds : new Bounds(Vector3.up * 0.5f, Vector3.one);
        }

        private static bool HasAny(GameObject[] prefabs)
        {
            if (prefabs == null) return false;
            foreach (GameObject prefab in prefabs)
            {
                if (prefab != null) return true;
            }
            return false;
        }

        private static GameObject Pick(GameObject[] prefabs)
        {
            GameObject prefab = null;
            while (prefab == null) prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Length)];
            return prefab;
        }

#if UNITY_EDITOR
        public const string DecorFolder = "Assets/Decor";

        public bool AutoAssignFromFolder()
        {
            bool changed = false;
            changed |= AssignIfEmpty(ref standingProps, standingPropNames);
            changed |= AssignIfEmpty(ref wallMountedProps, wallMountedPropNames);
            changed |= AssignIfEmpty(ref floorPanels, floorPanelNames);
            return changed;
        }

        public static GameObject LoadDecor(string propName)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{DecorFolder}/{propName}.fbx");
        }

        private static bool AssignIfEmpty(ref GameObject[] slots, string[] names)
        {
            if ((slots != null && slots.Length > 0) || names == null || names.Length == 0) return false;

            List<GameObject> found = new List<GameObject>();
            foreach (string propName in names)
            {
                GameObject asset = LoadDecor(propName);
                if (asset != null) found.Add(asset);
            }
            if (found.Count == 0) return false;

            slots = found.ToArray();
            return true;
        }
#endif
    }
}
