using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuardiasDaLuz.MapSystem
{
    /// <summary>
    /// Kit modular de cenário de um Nível (piso, paredes, porta, poste).
    /// As peças são escaladas para caber nas medidas das salas do gerador, então colisões e portas continuam iguais.
    /// No editor, os slots vazios são preenchidos automaticamente a partir da pasta do kit (ex.: Assets/Levels/L1_Lab).
    /// </summary>
    [Serializable]
    public class LevelVisualTheme
    {
        public string themeName = "Laboratório";
        [Tooltip("Pasta com os FBX do kit. No editor, os slots vazios abaixo são preenchidos automaticamente a partir dela.")]
        public string assetFolder = "Assets/Levels/L1_Lab";
        [Tooltip("Nome do piso principal das salas comuns.")]
        public string mainFloorName = "Lab_Floor_Plain";
        [Tooltip("Nome do piso das salas especiais (Boss, Loja, Item Único e Secreta).")]
        public string accentFloorName = "Lab_Floor_Grate";

        [Header("Peças do kit (preenchidas automaticamente no editor)")]
        public GameObject mainFloor;
        public GameObject accentFloor;
        public GameObject[] floorDetails;
        public GameObject mainWall;
        public GameObject[] decoratedWalls;
        public GameObject wallDoor;
        public GameObject wallCorner;
        public GameObject post;

        [Header("Distribuição")]
        [Tooltip("Tamanho aproximado de cada ladrilho do piso em metros (ajustado para fechar a sala certinho).")]
        public float floorTileSize = 4f;
        [Range(0f, 1f)] public float floorDetailChance = 0.18f;
        [Range(0f, 1f)] public float decoratedWallChance = 0.2f;
        [Tooltip("Fração da largura da peça de porta que é vão livre (o resto são os batentes).")]
        [Range(0.1f, 1f)] public float doorOpeningRatio = 0.5f;

        [NonSerialized] private Dictionary<GameObject, Vector3> meshSizeCache;

        public bool IsUsable => mainFloor != null && mainWall != null;

        // ================================================================================================
        // PISO
        // ================================================================================================
        public void BuildFloor(Transform parent, float width, float length, bool specialRoom)
        {
            GameObject baseTile = specialRoom && accentFloor != null ? accentFloor : mainFloor;
            int countX = Mathf.Max(1, Mathf.RoundToInt(width / floorTileSize));
            int countZ = Mathf.Max(1, Mathf.RoundToInt(length / floorTileSize));
            float tileX = width / countX;
            float tileZ = length / countZ;

            for (int i = 0; i < countX; i++)
            {
                for (int j = 0; j < countZ; j++)
                {
                    GameObject prefab = baseTile;
                    if (!specialRoom && floorDetails != null && floorDetails.Length > 0 && UnityEngine.Random.value < floorDetailChance)
                    {
                        GameObject detail = floorDetails[UnityEngine.Random.Range(0, floorDetails.Length)];
                        if (detail != null) prefab = detail;
                    }

                    Vector3 size = GetMeshSize(prefab);
                    Vector3 localPos = new Vector3(-width * 0.5f + tileX * (i + 0.5f), 0f, -length * 0.5f + tileZ * (j + 0.5f));
                    // Só gira 0° ou 180° para o ladrilho continuar fechando a grade quando não é quadrado
                    Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.value < 0.5f ? 0f : 180f, 0f);
                    Spawn(prefab, parent, localPos, rotation, new Vector3(tileX / size.x, 1f, tileZ / size.z), "Piso_Kit");
                }
            }
        }

        // ================================================================================================
        // PAREDES
        // ================================================================================================
        /// <param name="localBase">Centro da parede na base (y = chão), no espaço do parent.</param>
        /// <param name="inward">Direção para onde a face decorada (+Z da peça) deve apontar.</param>
        public void BuildWall(Transform parent, Vector3 localBase, float length, bool alongX, Vector3 inward,
            float height, float thickness, bool forceDecorated = false, float tint = 1f)
        {
            Vector3 reference = GetMeshSize(mainWall);
            float scaleY = height / reference.y;
            float scaleZ = thickness / reference.z;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / (reference.x * scaleY)));
            float slot = length / count;
            float scaleX = slot / reference.x;

            Quaternion rotation = Quaternion.LookRotation(inward, Vector3.up);
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;

            for (int k = 0; k < count; k++)
            {
                GameObject prefab = mainWall;
                bool decorate = forceDecorated || UnityEngine.Random.value < decoratedWallChance;
                if (decorate && decoratedWalls != null && decoratedWalls.Length > 0)
                {
                    GameObject decorated = decoratedWalls[UnityEngine.Random.Range(0, decoratedWalls.Length)];
                    if (decorated != null) prefab = decorated;
                }

                Vector3 localPos = localBase + axis * (-length * 0.5f + slot * (k + 0.5f));
                GameObject piece = Spawn(prefab, parent, localPos, rotation, new Vector3(scaleX, scaleY, scaleZ), "Parede_Kit");
                if (tint != 1f) Tint(piece, tint);
            }
        }

        // ================================================================================================
        // MOLDURA DE PORTA (o vão fica livre; os batentes ganham collider)
        // ================================================================================================
        public GameObject BuildDoorFrame(Transform parent, Vector3 worldBase, float gapWidth, bool alongX,
            float height, float thickness, out float openingWidth)
        {
            Vector3 size = GetMeshSize(wallDoor);
            Vector3 reference = GetMeshSize(mainWall);
            float scaleX = gapWidth / size.x;

            GameObject frame = Spawn(wallDoor, parent, Vector3.zero, Quaternion.identity,
                new Vector3(scaleX, height / size.y, thickness / reference.z), "Moldura_Porta_Kit");
            frame.transform.SetPositionAndRotation(worldBase, alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));

            // Colliders dos batentes no espaço da malha (a escala do objeto ajusta o tamanho final)
            float openHalf = size.x * doorOpeningRatio * 0.5f;
            float jambWidth = size.x * 0.5f - openHalf;
            foreach (float side in new[] { -1f, 1f })
            {
                BoxCollider jamb = frame.AddComponent<BoxCollider>();
                jamb.center = new Vector3(side * (openHalf + jambWidth * 0.5f), size.y * 0.5f, 0f);
                jamb.size = new Vector3(jambWidth, size.y, reference.z);
            }

            openingWidth = size.x * doorOpeningRatio * scaleX;
            return frame;
        }

        // ================================================================================================
        // POSTES (pilares nos cantos das salas)
        // ================================================================================================
        public void PlacePost(Transform parent, Vector3 worldBase, float wallHeight, float wallThickness)
        {
            if (post == null) return;

            Vector3 reference = GetMeshSize(mainWall);
            float scaleY = wallHeight / reference.y;
            float scaleXZ = wallThickness / reference.z;
            GameObject pillar = Spawn(post, parent, Vector3.zero, Quaternion.identity, new Vector3(scaleXZ, scaleY, scaleXZ), "Pilar_Kit");
            pillar.transform.position = worldBase;
        }

        // ================================================================================================
        // UTILITÁRIOS
        // ================================================================================================
        private GameObject Spawn(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale, string name)
        {
            GameObject obj = UnityEngine.Object.Instantiate(prefab, parent);
            obj.name = name;
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = localRot;
            obj.transform.localScale = scale;
            return obj;
        }

        private Vector3 GetMeshSize(GameObject prefab)
        {
            if (meshSizeCache == null) meshSizeCache = new Dictionary<GameObject, Vector3>();
            if (meshSizeCache.TryGetValue(prefab, out Vector3 cached)) return cached;

            Vector3 size = Vector3.one;
            MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                size = filter.sharedMesh.bounds.size;
                size = new Vector3(Mathf.Max(size.x, 0.01f), Mathf.Max(size.y, 0.01f), Mathf.Max(size.z, 0.01f));
            }
            meshSizeCache[prefab] = size;
            return size;
        }

        private static void Tint(GameObject obj, float factor)
        {
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
            {
                foreach (Material m in r.materials)
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * factor);
                    else if (m.HasProperty("_Color")) m.color *= factor;
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Preenche os slots vazios com os FBX da pasta do kit, pelo padrão de nomes:
        /// *_Floor_* (pisos), *_Wall (parede), *_Wall_Door, *_Wall_Corner, *_Post e *_Wall_* (paredes decoradas).
        /// Retorna true se algo foi preenchido.
        /// </summary>
        public bool AutoAssignFromFolder()
        {
            bool needsFloors = floorDetails == null || floorDetails.Length == 0;
            bool needsDecor = decoratedWalls == null || decoratedWalls.Length == 0;
            bool missing = mainFloor == null || accentFloor == null || mainWall == null || wallDoor == null
                           || wallCorner == null || post == null || needsFloors || needsDecor;
            if (!missing || string.IsNullOrEmpty(assetFolder) || !UnityEditor.AssetDatabase.IsValidFolder(assetFolder))
                return false;

            List<GameObject> floors = new List<GameObject>();
            List<GameObject> decor = new List<GameObject>();
            bool changed = false;

            foreach (string file in System.IO.Directory.GetFiles(assetFolder))
            {
                string path = file.Replace('\\', '/');
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                string n = asset.name;

                if (n == mainFloorName) changed |= AssignIfEmpty(ref mainFloor, asset);
                else if (n == accentFloorName) changed |= AssignIfEmpty(ref accentFloor, asset);
                else if (n.Contains("_Floor_")) floors.Add(asset);
                else if (n.EndsWith("_Wall_Door")) changed |= AssignIfEmpty(ref wallDoor, asset);
                else if (n.EndsWith("_Wall_Corner")) changed |= AssignIfEmpty(ref wallCorner, asset);
                else if (n.EndsWith("_Post")) changed |= AssignIfEmpty(ref post, asset);
                else if (n.EndsWith("_Wall")) changed |= AssignIfEmpty(ref mainWall, asset);
                else if (n.Contains("_Wall_")) decor.Add(asset);
            }

            if (needsFloors && floors.Count > 0) { floorDetails = floors.ToArray(); changed = true; }
            if (needsDecor && decor.Count > 0) { decoratedWalls = decor.ToArray(); changed = true; }
            return changed;
        }

        private static bool AssignIfEmpty(ref GameObject slot, GameObject asset)
        {
            if (slot != null) return false;
            slot = asset;
            return true;
        }
#endif
    }
}
