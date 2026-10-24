using GuardiasDaLuz.MapSystem;
using UnityEngine;

namespace GuardiasDaLuz
{
    /// <summary>
    /// A parceira de outra máquina no modo cooperativo: uma cópia só visual da personagem local (malhas e
    /// materiais, sem scripts), com a luz e o anel na cor da vaga dela, o nome acima da cabeça e os tiros
    /// repetidos a partir dos pacotes SHOT (os projéteis ferem os monstros desta máquina).
    /// </summary>
    public class CoopRemoteAvatar : MonoBehaviour
    {
        public int playerId;
        public string nickname;
        public Color slotColor = Color.white;
        public float health = 100f;
        public float maxHealth = 100f;
        public bool isDead;
        public bool isMoving;
        public WeaponType weapon;
        public int floorIndex;

        private Transform visual;
        private Light auraLight;
        private GameObject ring;
        private float bob;
        private bool visible = true;
        private GUIStyle nameStyle;

        public bool IsVisible => visible;

        public static CoopRemoteAvatar Create(int playerId, string nickname, Color color, Transform localPlayer)
        {
            GameObject root = new GameObject($"Guardiã {playerId + 1} ({nickname})");
            CoopRemoteAvatar avatar = root.AddComponent<CoopRemoteAvatar>();
            avatar.playerId = playerId;
            avatar.nickname = nickname;
            avatar.slotColor = color;
            if (localPlayer != null)
            {
                root.transform.SetPositionAndRotation(localPlayer.position + new Vector3((playerId - 1.5f) * 2.2f, 0f, 0f), localPlayer.rotation);
            }
            avatar.Build(localPlayer);
            return avatar;
        }

        private void Build(Transform source)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            if (source != null)
            {
                foreach (MeshRenderer r in source.GetComponentsInChildren<MeshRenderer>())
                {
                    MeshFilter filter = r.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || !r.enabled) continue;
                    GameObject part = new GameObject(r.name);
                    part.transform.SetParent(visual, false);
                    part.transform.localPosition = source.InverseTransformPoint(r.transform.position);
                    part.transform.localRotation = Quaternion.Inverse(source.rotation) * r.transform.rotation;
                    part.transform.localScale = DivideScale(r.transform.lossyScale, source.lossyScale);
                    part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                }
            }

            if (visual.childCount == 0)
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(capsule.GetComponent<Collider>());
                capsule.transform.SetParent(visual, false);
                capsule.transform.localPosition = Vector3.up;
                Renderer r = capsule.GetComponent<Renderer>();
                r.material.color = slotColor;
            }

            // anel no chão na cor da vaga
            ring = new GameObject("Anel_Parceira");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            LineRenderer ringLine = ring.AddComponent<LineRenderer>();
            ringLine.useWorldSpace = false;
            ringLine.loop = true;
            ringLine.widthMultiplier = 0.08f;
            ringLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringLine.material = new Material(Shader.Find("Sprites/Default"));
            ringLine.startColor = ringLine.endColor = new Color(slotColor.r, slotColor.g, slotColor.b, 0.8f);
            const int ringSegments = 40;
            ringLine.positionCount = ringSegments;
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ringSegments;
                ringLine.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.65f, 0f, Mathf.Sin(angle) * 0.65f));
            }

            GameObject auraObj = new GameObject($"Aura_Luz_P{playerId + 1}");
            auraObj.transform.SetParent(transform, false);
            auraObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            auraLight = auraObj.AddComponent<Light>();
            auraLight.type = LightType.Point;
            auraLight.color = slotColor;
            auraLight.range = 9f;
            auraLight.intensity = 2.5f;
            LightField.Ignore(auraLight);
        }

        private static Vector3 DivideScale(Vector3 a, Vector3 b) =>
            new Vector3(a.x / Mathf.Max(1e-4f, b.x), a.y / Mathf.Max(1e-4f, b.y), a.z / Mathf.Max(1e-4f, b.z));

        public void SetVisible(bool on)
        {
            if (visible == on) return;
            visible = on;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            if (auraLight != null) auraLight.enabled = on;
        }

        private void Update()
        {
            if (visual == null) return;
            // caminhada simples: balanço enquanto anda; caída de lado quando morta
            bob = isMoving && !isDead ? bob + Time.deltaTime * 10f : Mathf.MoveTowards(bob, 0f, Time.deltaTime * 6f);
            float lean = isDead ? 85f : 0f;
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(lean, 0f, Mathf.Sin(bob) * 4f), Time.deltaTime * 8f);
            visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bob)) * 0.06f, 0f);
            if (auraLight != null) auraLight.intensity = isDead ? 0.6f : 2.5f;
        }

        /// <summary>Repete localmente o disparo da parceira com o padrão da arma dela.</summary>
        public void ReplayShot(Vector3 origin, Vector3 forward, WeaponType weaponType, PlayerShooting localShooting)
        {
            GameObject prefab = localShooting != null ? localShooting.BulletPrefab : null;
            if (prefab == null) return;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
            Quaternion aim = Quaternion.LookRotation(forward.normalized);

            switch (weaponType)
            {
                case WeaponType.AimedTriple:
                    for (int i = -1; i <= 1; i++) Fire(prefab, origin, aim * Quaternion.Euler(0f, i * 12f, 0f));
                    break;
                case WeaponType.EightWayBurst:
                    for (int i = 0; i < 8; i++) Fire(prefab, origin, aim * Quaternion.Euler(0f, i * 45f, 0f));
                    break;
                case WeaponType.Shotgun:
                    for (int i = 0; i < 7; i++) Fire(prefab, origin, aim * Quaternion.Euler(0f, Mathf.Lerp(-15f, 15f, i / 6f), 0f));
                    break;
                case WeaponType.SineWave:
                    LightBullet wave = Fire(prefab, origin, aim);
                    if (wave != null) wave.ConfigureWave(0.8f, 3f);
                    break;
                case WeaponType.Exploding:
                    LightBullet heavy = Fire(prefab, origin, aim);
                    if (heavy != null)
                    {
                        heavy.SetSpeed(12f);
                        heavy.SetDamage(20f);
                    }
                    break;
                case WeaponType.RapidFire:
                    LightBullet quick = Fire(prefab, origin, aim * Quaternion.Euler(0f, Random.Range(-3f, 3f), 0f));
                    if (quick != null) quick.SetDamage(5f);
                    break;
                case WeaponType.Homing:
                    foreach (float side in new[] { -1f, 1f })
                    {
                        LightBullet seeker = Fire(prefab, origin, aim * Quaternion.Euler(0f, side * 16f, 0f));
                        if (seeker == null) continue;
                        seeker.SetSpeed(15f);
                        seeker.SetDamage(14f);
                        seeker.SetLifetime(3.5f);
                        seeker.ConfigureHoming(260f);
                    }
                    break;
            }
        }

        private static LightBullet Fire(GameObject prefab, Vector3 origin, Quaternion rotation)
        {
            GameObject bullet = Instantiate(prefab, origin, rotation);
            return bullet.GetComponent<LightBullet>();
        }

        private void OnGUI()
        {
            HudScale.Begin();
            DrawHud();
            HudScale.End();
        }

        private void DrawHud()
        {
            if (!visible) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.4f);
            if (screen.z <= 0f) return;

            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            string text = isDead ? $"{nickname} (caída)" : nickname;
            Vector2 point = HudScale.FromScreenPoint(screen);
            Rect rect = new Rect(point.x - 80f, point.y - 12f, 160f, 22f);
            nameStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, nameStyle);
            nameStyle.normal.textColor = slotColor;
            GUI.Label(rect, text, nameStyle);

            Rect bar = new Rect(point.x - 30f, point.y + 9f, 60f, 5f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.9f, 0.4f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(health / Mathf.Max(1f, maxHealth)), bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
