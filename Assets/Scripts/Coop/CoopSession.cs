using GuardiasDaLuz.MapSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardiasDaLuz
{
    /// <summary>
    /// Estado da partida cooperativa entre o menu e o jogo (no projeto original, guardado no LuMinasMainMenu):
    /// apelido, semente da dungeon (igual para todas as jogadoras), se a expedição já começou e o log de conexão
    /// mostrado no painel Multiplayer do menu.
    /// </summary>
    public static class CoopSession
    {
        public enum PlayModeChoice
        {
            Singleplayer,
            MultiplayerCoop
        }

        public const string GameScene = "SampleScene";
        public const string MenuScene = "Menu";

        public static PlayModeChoice selectedPlayMode = PlayModeChoice.Singleplayer;
        public static string playerNickname = "Guardiã";
        public static string customDungeonSeed = "";
        public static bool hasStartedRun;
        public static string multiplayerConnectionLog = "";

        public static bool IsCoop => LunaNetworkSyncManager.Instance != null && LunaNetworkSyncManager.Instance.IsConnected;

        /// <summary>Semente de cada andar: hash estável da semente da partida (igual em todas as máquinas).</summary>
        public static int SeedFor(int floorIndex)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in customDungeonSeed ?? "")
                {
                    hash = (hash ^ c) * 16777619;
                }
                return hash + floorIndex * 10007;
            }
        }

        public static string NewSeed() => Random.Range(100000, 999999).ToString();

        /// <summary>Host: sorteia a semente, avisa as clientes e carrega o jogo.</summary>
        public static void StartExpedition()
        {
            if (string.IsNullOrWhiteSpace(customDungeonSeed)) customDungeonSeed = NewSeed();
            selectedPlayMode = PlayModeChoice.MultiplayerCoop;
            hasStartedRun = true;
            StandaloneLighthouseMapSystem.FloorSeedProvider = SeedFor;
            if (LunaNetworkSyncManager.Instance != null) LunaNetworkSyncManager.Instance.NotifyStartRun(customDungeonSeed);
            SceneManager.LoadScene(GameScene);
        }

        /// <summary>Cliente: o host começou (START_GAME) com esta semente.</summary>
        public static void StartExpeditionFromNetwork()
        {
            selectedPlayMode = PlayModeChoice.MultiplayerCoop;
            hasStartedRun = true;
            StandaloneLighthouseMapSystem.FloorSeedProvider = SeedFor;
            SceneManager.LoadScene(GameScene);
        }

        /// <summary>Encerra o modo cooperativo e volta ao menu.</summary>
        public static void TerminateMultiplayerSession()
        {
            if (LunaNetworkSyncManager.Instance != null) LunaNetworkSyncManager.Instance.StopNetwork();
            ResetToSingleplayer();
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuScene);
        }

        public static void ResetToSingleplayer()
        {
            hasStartedRun = false;
            selectedPlayMode = PlayModeChoice.Singleplayer;
            customDungeonSeed = "";
            StandaloneLighthouseMapSystem.FloorSeedProvider = null;
        }
    }
}
