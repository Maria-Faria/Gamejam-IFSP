using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Roguelike.Tests
{
    /// The multiplayer and accessibility modules (from the GameJAM project, adapted to this game): the UDP
    /// handshake of LunaNetworkSyncManager as host and as client (against a raw socket standing in for the other
    /// side), the shared dungeon seed giving every player the same floor, and the accessibility HUD's options.
    public class CoopNetworkTests
    {
        static System.Type GameType(string name) => System.Type.GetType($"{name}, Assembly-CSharp", true);
        static readonly System.Type Net = System.Type.GetType("GuardiasDaLuz.LunaNetworkSyncManager, Assembly-CSharp", true);
        static readonly System.Type Session = System.Type.GetType("GuardiasDaLuz.CoopSession, Assembly-CSharp", true);

        Component _net;
        UdpClient _peer;

        Component Manager()
        {
            _net = (Component)Net.GetMethod("EnsureExists").Invoke(null, null);
            return _net;
        }

        static void Send(UdpClient peer, string msg, IPEndPoint to)
        {
            var bytes = Encoding.UTF8.GetBytes(msg);
            peer.Send(bytes, bytes.Length, to);
        }

        static IEnumerator Receive(UdpClient peer, List<string> into, string prefix, float seconds)
        {
            for (float end = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < end;)
            {
                while (peer.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    into.Add(Encoding.UTF8.GetString(peer.Receive(ref from)));
                }
                if (into.Any(m => m.StartsWith(prefix))) yield break;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HostWelcomesAClientAndSeesItLeave()
        {
            var net = Manager();
            Session.GetField("customDungeonSeed").SetValue(null, "TESTE-123");
            Net.GetMethod("StartHost").Invoke(net, new object[] { 47771 });
            _peer = new UdpClient(0);
            var host = new IPEndPoint(IPAddress.Loopback, 47771);
            Send(_peer, "HELLO|Parceira", host);

            var got = new List<string>();
            yield return Receive(_peer, got, "WELCOME", 3f);
            var welcome = got.FirstOrDefault(m => m.StartsWith("WELCOME"));
            Assert.IsNotNull(welcome, "the host should answer WELCOME");
            Assert.AreEqual("WELCOME|1|TESTE-123", string.Join("|", welcome.Split('|').Take(3)), "slot 1 and the shared seed");
            Assert.IsTrue((bool)Net.GetProperty("IsConnected").GetValue(net));
            Assert.AreEqual(1, Net.GetProperty("ConnectedPeersCount").GetValue(net));

            Send(_peer, "DISCONNECT|1|Parceira", host);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(0, Net.GetProperty("ConnectedPeersCount").GetValue(net), "the slot frees up again");
        }

        [UnityTest]
        public IEnumerator ClientJoinsAHostAndTakesItsSeed()
        {
            var net = Manager();
            Session.GetField("playerNickname").SetValue(null, "Cliente");
            _peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 47773));
            Net.GetMethod("StartClient").Invoke(net, new object[] { "127.0.0.1", 47773, "Cliente" });

            // the fake host reads the HELLO and answers to the port it came from
            var clientEnd = new IPEndPoint(IPAddress.Any, 0);
            string hello = null;
            for (float end = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < end && hello == null;)
            {
                if (_peer.Available > 0) hello = Encoding.UTF8.GetString(_peer.Receive(ref clientEnd));
                else yield return null;
            }
            Assert.AreEqual("HELLO|Cliente", hello, "the client says HELLO with its nickname");
            Send(_peer, "WELCOME|2|SEMENTE-9|Anfitria", clientEnd);
            for (float end = Time.realtimeSinceStartup + 2f; Time.realtimeSinceStartup < end && !(bool)Net.GetProperty("IsConnected").GetValue(net);)
                yield return null;
            Assert.IsTrue((bool)Net.GetProperty("IsConnected").GetValue(net), "connected after WELCOME");
            Assert.AreEqual(2, Net.GetProperty("MyPlayerId").GetValue(net), "it takes the slot the host gave");
            Assert.AreEqual("SEMENTE-9", Session.GetField("customDungeonSeed").GetValue(null), "and the host's seed");

            var got = new List<string>();
            yield return Receive(_peer, got, "CLIENT_READY", 2f);
            Assert.IsTrue(got.Any(m => m.StartsWith("CLIENT_READY|2|")), "it reports ready");
        }

        [Test]
        public void SeedsAreStableAndDifferPerFloor()
        {
            Session.GetField("customDungeonSeed").SetValue(null, "PROJETO-UV");
            var seedFor = Session.GetMethod("SeedFor");
            int a = (int)seedFor.Invoke(null, new object[] { 0 });
            Assert.AreEqual(a, (int)seedFor.Invoke(null, new object[] { 0 }));
            Assert.AreNotEqual(a, (int)seedFor.Invoke(null, new object[] { 1 }));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_peer != null) _peer.Close();
            _peer = null;
            if (_net != null)
            {
                Net.GetMethod("StopNetwork").Invoke(_net, null);
                Object.Destroy(_net.gameObject);
            }
            Session.GetMethod("ResetToSingleplayer").Invoke(null, null);
            yield return null;
        }
    }

#if UNITY_EDITOR
    /// A host inside SampleScene with a raw socket as its partner: the partner's state makes an avatar, its
    /// shots fly as light bullets, its repaired post and cleared room show up here, entering a room pulls the
    /// local player in, and the host streams its own state back.
    public class CoopGameSceneTests
    {
        static System.Type GameType(string name) => System.Type.GetType($"{name}, Assembly-CSharp", true);
        static readonly System.Type Net = System.Type.GetType("GuardiasDaLuz.LunaNetworkSyncManager, Assembly-CSharp", true);
        static readonly System.Type Session = System.Type.GetType("GuardiasDaLuz.CoopSession, Assembly-CSharp", true);
        static readonly System.Type MapType = System.Type.GetType("GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem, Assembly-CSharp", true);

        Scene _game;
        Component _net;
        UdpClient _peer;

        static void Send(UdpClient peer, string msg, IPEndPoint to)
        {
            var bytes = Encoding.UTF8.GetBytes(msg);
            peer.Send(bytes, bytes.Length, to);
        }

        static string F(float v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        static object Field(object o, string name) => o.GetType().GetField(name).GetValue(o);

        static IEnumerable<object> Rooms(object map)
        {
            foreach (var kv in (IEnumerable)MapType.GetProperty("DungeonGrid").GetValue(map))
                yield return kv.GetType().GetProperty("Value").GetValue(kv);
        }

        [UnityTest]
        public IEnumerator HostAppliesThePartnersPackets()
        {
            _net = (Component)Net.GetMethod("EnsureExists").Invoke(null, null);
            Session.GetField("customDungeonSeed").SetValue(null, "COOP-TESTE");
            Net.GetMethod("StartHost").Invoke(_net, new object[] { 47774 });
            Session.GetField("hasStartedRun").SetValue(null, true);
            var provider = System.Delegate.CreateDelegate(typeof(System.Func<int, int>), Session.GetMethod("SeedFor"));
            MapType.GetField("FloorSeedProvider").SetValue(null, provider);

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            _game = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
            SceneManager.SetActiveScene(_game);
            for (float end = Time.time + 1f; Time.time < end;)
                yield return null;

            var map = MapType.GetProperty("Instance").GetValue(null);
            var local = (Transform)Net.GetMethod("FindLocalPlayer").Invoke(null, null);
            Assert.IsNotNull(local);
            var bridge = local.GetComponent<Health>() ?? (Health)local.gameObject.AddComponent(GameType("MonsterPlayerHealth"));
            bridge.Invulnerable = true;

            _peer = new UdpClient(0);
            var host = new IPEndPoint(IPAddress.Loopback, 47774);
            Send(_peer, "HELLO|Parceira", host);
            for (int i = 0; i < 20; i++) yield return null;

            // the partner's state: an avatar appears where it stands, with its health
            var at = local.position + new Vector3(2f, 0f, 1f);
            Send(_peer, $"PLYR|1|{F(at.x)}|{F(at.y)}|{F(at.z)}|90.0|80.0|100.0|50.0|100.0|0|0|1|0", host);
            object avatar = null;
            for (float end = Time.realtimeSinceStartup + 2f; Time.realtimeSinceStartup < end && avatar == null;)
            {
                yield return null;
                avatar = ((IEnumerable)Net.GetMethod("RemoteAvatars").Invoke(_net, null)).Cast<object>().FirstOrDefault();
            }
            Assert.IsNotNull(avatar, "the partner gets an avatar");
            yield return null;
            Assert.AreEqual(80f, (float)Field(avatar, "health"), 1e-3f);
            Assert.Less(Vector3.Distance(((Component)avatar).transform.position, at), 0.5f);

            // its shot is replayed as light bullets
            int bulletsBefore = Object.FindObjectsByType(GameType("LightBullet")).Length;
            Send(_peer, $"SHOT|1|{F(at.x)}|{F(at.y + 1f)}|{F(at.z)}|1.000|0.000|0.000|0", host);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(bulletsBefore + 3, Object.FindObjectsByType(GameType("LightBullet")).Length, "a triple shot");

            // its repaired post is repaired here too
            var start = Rooms(map).First(r => Field(r, "roomType").ToString() == "Start");
            var post = (Component)((IList)Field(start, "roomPosts"))[0];
            var pp = post.transform.position;
            Send(_peer, $"POST|{F(pp.x)}|{F(pp.y)}|{F(pp.z)}", host);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue((bool)Field(post, "isRepaired"), "the partner's post");

            // the partner walks into a combat room: the local player is pulled in with it
            var room = Rooms(map).First(r => Field(r, "roomType").ToString() == "Combat");
            var coord = (Vector2Int)Field(room, "gridCoord");
            var centre = (Vector3)Field(room, "worldCenter");
            Send(_peer, $"ROOM_ENTER|{coord.x}|{coord.y}|{F(centre.x)}|1.00|{F(centre.z - 9f)}|0", host);
            for (float end = Time.realtimeSinceStartup + 2f; Time.realtimeSinceStartup < end && (Vector2Int)MapType.GetField("currentPlayerRoomCoord").GetValue(map) != coord;)
                yield return null;
            Assert.AreEqual(coord, (Vector2Int)MapType.GetField("currentPlayerRoomCoord").GetValue(map), "regrouped into the partner's room");

            // monsters come; the partner clears the room: they are gone here and the doors open
            var root = (GameObject)Field(room, "roomRoot");
            for (float end = Time.time + 4f; Time.time < end && root.GetComponentsInChildren<Monster>().Length == 0;)
                yield return null;
            Assert.Greater(root.GetComponentsInChildren<Monster>().Length, 0);
            Send(_peer, $"ROOM_CLEARED|{coord.x}|{coord.y}|0", host);
            for (float end = Time.time + 3f; Time.time < end && !(bool)Field(room, "doorsUnlocked");)
                yield return null;
            Assert.IsTrue((bool)Field(room, "doorsUnlocked"), "the room the partner won opens here too");

            // and the host streams its own state to the partner
            var got = new List<string>();
            for (float end = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < end && !got.Any(m => m.StartsWith("PLYR|0|"));)
            {
                while (_peer.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    got.Add(Encoding.UTF8.GetString(_peer.Receive(ref from)));
                }
                yield return null;
            }
            Assert.IsTrue(got.Any(m => m.StartsWith("PLYR|0|")), "the host sends its state");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_peer != null) _peer.Close();
            _peer = null;
            if (_net != null)
            {
                Net.GetMethod("StopNetwork").Invoke(_net, null);
                Object.Destroy(_net.gameObject);
            }
            Session.GetMethod("ResetToSingleplayer").Invoke(null, null);
            if (!_game.IsValid() || !_game.isLoaded)
                yield break;
            var empty = SceneManager.CreateScene("AfterCoopScene" + Random.Range(0, 99999));
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(_game);
        }
    }
#endif

#if UNITY_EDITOR
    public class AccessibilitySceneTests
    {
        static readonly System.Type MapType = System.Type.GetType("GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem, Assembly-CSharp", true);
        static readonly System.Type Hud = System.Type.GetType("GuardiasDaLuz.MultiplayerAndAccessibilityHUD, Assembly-CSharp", true);
        static readonly System.Type Session = System.Type.GetType("GuardiasDaLuz.CoopSession, Assembly-CSharp", true);

        Scene _game;

        IEnumerator LoadGame()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            _game = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
            SceneManager.SetActiveScene(_game);
            for (float end = Time.time + 1f; Time.time < end;)
                yield return null;
        }

        static IEnumerable<(Vector2Int, string)> Layout(object map)
        {
            foreach (var kv in (IEnumerable)MapType.GetProperty("DungeonGrid").GetValue(map))
            {
                var node = kv.GetType().GetProperty("Value").GetValue(kv);
                yield return ((Vector2Int)node.GetType().GetField("gridCoord").GetValue(node), node.GetType().GetField("roomType").GetValue(node).ToString());
            }
        }

        [UnityTest]
        public IEnumerator HudOptionsApply()
        {
            yield return LoadGame();
            var hud = Hud.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(hud, "the HUD is created in the game scene");
            var map = MapType.GetProperty("Instance").GetValue(null);

            Hud.GetMethod("ApplyGameSpeed").Invoke(hud, new object[] { 0.5f });
            Assert.AreEqual(0.5f, Time.timeScale, 1e-4f);
            Hud.GetMethod("ApplyGameSpeed").Invoke(hud, new object[] { 1f });
            Assert.AreEqual(1f, Time.timeScale, 1e-4f);

            Hud.GetMethod("ApplySubtitles").Invoke(hud, new object[] { false });
            Assert.IsFalse((bool)MapType.GetField("showBuiltInSubtitleHUD").GetValue(map));
            Hud.GetMethod("ApplySubtitles").Invoke(hud, new object[] { true });

            Hud.GetMethod("ApplyColorblindMode").Invoke(hud, new object[] { 1, false });
            var volume = ((Component)hud).GetComponentInChildren<Volume>();
            Assert.IsNotNull(volume, "its own global volume");
            Assert.IsTrue(volume.profile.TryGet(out UnityEngine.Rendering.Universal.ChannelMixer mixer) && mixer.active);
            Assert.AreEqual(62.5f, mixer.redOutRedIn.value, 1e-3f, "deuteranopia matrix");
            Hud.GetMethod("ApplyColorblindMode").Invoke(hud, new object[] { 0, false });
            Assert.IsFalse(mixer.active);

            Hud.GetMethod("ApplyHighContrastMode").Invoke(hud, new object[] { true, false });
            Assert.IsTrue(volume.profile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments adj) && adj.active && adj.postExposure.value > 1f);
            Hud.GetMethod("ApplyHighContrastMode").Invoke(hud, new object[] { false, false });
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedSeedGivesTheSameFloor()
        {
            yield return LoadGame();
            var map = MapType.GetProperty("Instance").GetValue(null);
            Session.GetField("customDungeonSeed").SetValue(null, "MESMO-MAPA");
            var provider = System.Delegate.CreateDelegate(typeof(System.Func<int, int>), Session.GetMethod("SeedFor"));
            MapType.GetField("FloorSeedProvider").SetValue(null, provider);
            try
            {
                MapType.GetMethod("GenerateFloor").Invoke(map, new object[] { 2 });
                var first = Layout(map).OrderBy(r => r.Item1.x).ThenBy(r => r.Item1.y).ToList();
                yield return null;
                MapType.GetMethod("GenerateFloor").Invoke(map, new object[] { 2 });
                var second = Layout(map).OrderBy(r => r.Item1.x).ThenBy(r => r.Item1.y).ToList();
                CollectionAssert.AreEqual(first, second, "same seed, same rooms");
                Assert.Greater(first.Count, 10);
            }
            finally
            {
                MapType.GetField("FloorSeedProvider").SetValue(null, null);
            }
        }

        [UnityTearDown]
        public IEnumerator UnloadGame()
        {
            Time.timeScale = 1f;
            foreach (var key in new[] { "acc_velocidade", "acc_alto_contraste", "acc_legendas", "acc_daltonismo" })
                PlayerPrefs.DeleteKey(key);
            if (!_game.IsValid() || !_game.isLoaded)
                yield break;
            var empty = SceneManager.CreateScene("AfterAccessibilityScene" + Random.Range(0, 99999));
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(_game);
        }
    }
#endif
}
