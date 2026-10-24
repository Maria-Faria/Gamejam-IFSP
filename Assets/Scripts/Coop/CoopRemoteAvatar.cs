using GuardiasDaLuz.MapSystem;
using UnityEngine;

namespace GuardiasDaLuz
{
    /// <summary>
    /// A parceira de outra máquina no modo cooperativo: a cientista que ela escolheu (o mesmo modelo rigado,
    /// correndo e erguendo a pistola como a dela), com a luz e o anel na cor da vaga dela, o nome acima da
    /// cabeça e os tiros repetidos a partir dos pacotes SHOT (os projéteis ferem os monstros desta máquina).
    /// Sem os modelos gerados, é uma cópia só visual das malhas da personagem local.
    /// </summary>
    public class CoopRemoteAvatar : MonoBehaviour
    {
        public int playerId;
        public string nickname;
        public Color slotColor = Color.white;
        public float health = 100f;
        public float maxHealth = 100f;
        public float mana = 100f;
        public float maxMana = 100f;
        public bool isDead;
        public bool isMoving;
        public WeaponType weapon;
        public int floorIndex;
        public int character = -1;

        private Transform visual;
        private Light auraLight;
        private GameObject ring;
        private float bob;
        private bool visible = true;
        private GUIStyle nameStyle;
        private CharacterRig rig;
        private Vector3 lastPosition;
        private float lastShot = -10f;

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

        /// <summary>Mostra a parceira com o modelo da cientista `index` (CharacterRoster).</summary>
        public void UseCharacter(int index)
        {
            if (index == character && rig != null) return;
            CharacterRig prefab = CharacterRoster.LoadPrefab(index);
            if (prefab == null) return;
            character = index;
            for (int i = visual.childCount - 1; i >= 0; i--) Destroy(visual.GetChild(i).gameObject);
            rig = Instantiate(prefab, visual, false);
            rig.name = "Cientista";
            if (!visible) foreach (Renderer r in rig.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        private void Build(Transform source)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            lastPosition = transform.position;

            CharacterRig defaultRig = CharacterRoster.LoadPrefab(CharacterRoster.Selected);
            if (defaultRig != null)
            {
                rig = Instantiate(defaultRig, visual, false);
                rig.name = "Cientista";
            }
            else if (source != null)
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
            ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Anel_Parceira";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(1.3f, 0.01f, 1.3f);
            Renderer ringRenderer = ring.GetComponent<Renderer>();
            ringRenderer.material.color = new Color(slotColor.r, slotColor.g, slotColor.b, 0.6f);
            ringRenderer.material.EnableKeyword("_EMISSION");
            ringRenderer.material.SetColor("_EmissionColor", slotColor * 1.5f);

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
            if (rig != null)
            {
                // corrida em 8 direções pela velocidade dela, como a personagem local
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                Vector3 velocity = (transform.position - lastPosition) / dt;
                velocity.y = 0f;
                lastPosition = transform.position;
                Vector3 local = isDead ? Vector3.zero : transform.InverseTransformDirection(velocity) / 10f;
                rig.SetLocomotion(local, Time.deltaTime);
                rig.SetAim(!isDead && Time.time - lastShot < 0.7f);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(isDead ? 85f : 0f, 0f, 0f), Time.deltaTime * 8f);
                if (auraLight != null) auraLight.intensity = isDead ? 0.6f : 2.5f;
                return;
            }
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
            lastShot = Time.time;
            if (rig != null) rig.Kick(PlayerCharacter.KickOf(weaponType));

            switch (weaponType)
            {
                case WeaponType.Pistol:
                    LightBullet pistolShot = Fire(prefab, origin, aim);
                    if (pistolShot != null) pistolShot.SetDamage(11f);
                    break;
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
            }
        }

        private static LightBullet Fire(GameObject prefab, Vector3 origin, Quaternion rotation)
        {
            GameObject bullet = Instantiate(prefab, origin, rotation);
            return bullet.GetComponent<LightBullet>();
        }

        private void OnGUI()
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
            Rect rect = new Rect(screen.x - 80f, Screen.height - screen.y - 12f, 160f, 22f);
            nameStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, nameStyle);
            nameStyle.normal.textColor = slotColor;
            GUI.Label(rect, text, nameStyle);

            Rect bar = new Rect(screen.x - 30f, Screen.height - screen.y + 9f, 60f, 5f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.9f, 0.4f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(health / Mathf.Max(1f, maxHealth)), bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
