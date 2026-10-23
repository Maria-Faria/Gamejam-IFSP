using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using GuardiasDaLuz.MapSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardiasDaLuz
{
    /// <summary>
    /// GERENCIADOR DE REDE MULTIPLAYER LAN/ONLINE ("Projeto UV") — módulo do projeto GameJAM (vinikyo), adaptado
    /// para a personagem (PlayerMovement/PlayerHealth/PlayerShooting) e o mapa (StandaloneLighthouseMapSystem) deste jogo:
    /// - UDP Sockets nativos do .NET (System.Net.Sockets), sem pacotes externos.
    /// - ATÉ 4 JOGADORAS (Host + até 3 Clientes), cada uma com a sua cor:
    ///   • Jogadora 1 (Host): Âmbar • Jogadora 2: Ciano • Jogadora 3: Verde • Jogadora 4: Magenta
    /// - Sincroniza (30 Hz): posição/rotação, vida, mana, arma e se caiu; tiros (repetidos no avatar da parceira,
    ///   ferindo os monstros locais); semente da dungeon (o mesmo mapa para todas); postes consertados; entrada em
    ///   salas (o esquadrão é reagrupado); salas vencidas; mudança de andar; e o recomeço da expedição quando uma
    ///   guardiã cai (roguelike: todas voltam ao Andar 1-1 num mapa novo).
    /// - Os monstros são simulados em cada máquina; quando uma jogadora vence uma sala, os monstros dela somem nas outras.
    /// </summary>
    public class LunaNetworkSyncManager : MonoBehaviour
    {
        public static LunaNetworkSyncManager Instance { get; private set; }

        public enum NetworkRole
        {
            Offline = 0,
            Host = 1,
            Client = 2
        }

        public class RemotePlayerSlot
        {
            public int playerId;
            public string nickname;
            public IPEndPoint endPoint;
            public CoopRemoteAvatar avatar;
            public Vector3 targetPosition;
            public Quaternion targetRotation;
            public bool hasReceivedFirstPosition;
            public float lastPacketTime;
        }

        public static readonly Color[] PlayerSlotColors = new Color[]
        {
            new Color(1.0f, 0.90f, 0.35f), // Jogadora 1 (Host): Âmbar Solar
            new Color(0.25f, 0.92f, 1.0f),  // Jogadora 2 (Client 1): Azul Ciano
            new Color(0.35f, 1.0f, 0.55f),  // Jogadora 3 (Client 2): Verde Esmeralda
            new Color(0.95f, 0.45f, 0.85f)  // Jogadora 4 (Client 3): Rosa Magenta
        };

        [Header("=== ESTADO DA CONEXÃO MULTIPLAYER (ATÉ 4 JOGADORAS) ===")]
        public NetworkRole currentRole = NetworkRole.Offline;
        public bool isConnected = false;
        public bool IsConnected => isConnected;
        public int myPlayerId = 0; // Host = 0; Clientes = 1, 2, 3
        public int MyPlayerId => myPlayerId;
        public int networkPort = 7777;
        public string hostAddress = "127.0.0.1";

        public readonly Dictionary<int, RemotePlayerSlot> remotePlayers = new Dictionary<int, RemotePlayerSlot>();

        public int ConnectedPeersCount
        {
            get
            {
                if (currentRole == NetworkRole.Host)
                {
                    lock (clientLock)
                    {
                        return connectedClients.Count;
                    }
                }
                return currentRole == NetworkRole.Client && isConnected ? 1 : 0;
            }
        }

        private UdpClient udpSocket;
        private IPEndPoint hostEndPoint;
        private readonly Dictionary<int, IPEndPoint> connectedClients = new Dictionary<int, IPEndPoint>();
        private readonly object clientLock = new object();
        private Thread receiveThread;
        private volatile bool isRunning = false;

        private struct IncomingPacket
        {
            public string message;
            public IPEndPoint sender;
        }

        private readonly Queue<IncomingPacket> incomingPacketsQueue = new Queue<IncomingPacket>();
        private readonly object queueLock = new object();

        private float nextStateSendTime = 0f;
        private float nextHelloRetryTime = 0f;
        private float lastPacketReceivedTime = 0f;
        private const float STATE_SEND_INTERVAL = 0.033f; // ~30 pacotes por segundo

        // ligação com a cena do jogo (refeita a cada carregamento)
        private StandaloneLighthouseMapSystem boundMap;
        private PlayerHealth boundHealth;
        private PlayerShooting boundShooting;
        private bool applyingRemote;
        private int lastSyncedFloor = -1;
        private StandaloneLighthouseMapSystem.MapRoomNode watchedRoom;
        private bool watchedRoomWasOpen;
        private float pendingRestartTime = -1f;

        /// <summary>Cria o gerenciador (persistente entre cenas) se ainda não existir.</summary>
        public static LunaNetworkSyncManager EnsureExists()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("LunaNetworkSyncManager");
            return go.AddComponent<LunaNetworkSyncManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.runInBackground = true;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Update()
        {
            ProcessIncomingPacketsOnMainThread();

            // Cliente tenta de novo enquanto não recebe o WELCOME
            if (currentRole == NetworkRole.Client && !isConnected && isRunning && Time.unscaledTime >= nextHelloRetryTime)
            {
                nextHelloRetryTime = Time.unscaledTime + 1.0f;
                SendRawPacket($"HELLO|{CoopSession.playerNickname}");
            }

            if (pendingRestartTime > 0f && Time.unscaledTime >= pendingRestartTime)
            {
                pendingRestartTime = -1f;
                Time.timeScale = 1f;
                SceneManager.LoadScene(CoopSession.GameScene);
                return;
            }

            if (!isConnected || currentRole == NetworkRole.Offline) return;

            if (CoopSession.hasStartedRun)
            {
                if (currentRole == NetworkRole.Client)
                {
                    if (lastPacketReceivedTime > 0f && (Time.unscaledTime - lastPacketReceivedTime) > 8.0f)
                    {
                        HandlePeerDisconnected("A conexão com a sala de rede foi perdida (Timeout).");
                        return;
                    }
                }
                else
                {
                    List<int> timedOutPeers = null;
                    foreach (var kvp in remotePlayers)
                    {
                        if (kvp.Value != null && kvp.Value.lastPacketTime > 0f && (Time.unscaledTime - kvp.Value.lastPacketTime) > 8.0f)
                        {
                            if (timedOutPeers == null) timedOutPeers = new List<int>();
                            timedOutPeers.Add(kvp.Key);
                        }
                    }
                    if (timedOutPeers != null)
                    {
                        foreach (int pid in timedOutPeers)
                        {
                            lock (clientLock)
                            {
                                connectedClients.Remove(pid);
                            }
                            RemoveRemotePlayerAvatar(pid);
                            ForwardPacketToOtherClients($"DISCONNECT|{pid}|Jogadora", null);
                        }
                    }
                }
            }

            if (Time.unscaledTime >= nextStateSendTime)
            {
                nextStateSendTime = Time.unscaledTime + STATE_SEND_INTERVAL;
                SendLocalPlayerState();
            }

            InterpolateRemoteAvatarTransform();
            WatchCurrentRoomCleared();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopNetwork();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            StopNetwork();
        }

        // ====================================================================================================
        // LIGAÇÃO COM A CENA DO JOGO
        // ====================================================================================================

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            boundMap = null;
            boundHealth = null;
            boundShooting = null;
            watchedRoom = null;
            lastSyncedFloor = -1;
            foreach (var slot in remotePlayers.Values)
            {
                slot.hasReceivedFirstPosition = false;
            }
        }

        // Liga os eventos do mapa e da jogadora local assim que existirem na cena.
        private void BindGame()
        {
            if (boundMap == null)
            {
                boundMap = StandaloneLighthouseMapSystem.Instance;
                if (boundMap != null)
                {
                    if (boundMap.onFloorGenerated == null) boundMap.onFloorGenerated = new UnityEngine.Events.UnityEvent<int, int, string>();
                    if (boundMap.onRoomEntered == null) boundMap.onRoomEntered = new UnityEngine.Events.UnityEvent<Vector2Int, string>();
                    if (boundMap.onSmartPostRepaired == null) boundMap.onSmartPostRepaired = new UnityEngine.Events.UnityEvent<GameObject>();
                    boundMap.onFloorGenerated.AddListener(OnLocalFloorGenerated);
                    boundMap.onRoomEntered.AddListener(OnLocalRoomEntered);
                    boundMap.onSmartPostRepaired.AddListener(OnLocalPostRepaired);
                    lastSyncedFloor = boundMap.currentFloorIndex;
                    PlaceLocalPlayerInSlot();
                }
            }

            if (boundHealth == null)
            {
                Transform local = FindLocalPlayer();
                if (local != null)
                {
                    boundHealth = local.GetComponent<PlayerHealth>();
                    boundShooting = local.GetComponent<PlayerShooting>();
                    if (boundHealth != null) boundHealth.Died += OnLocalPlayerDied;
                    if (boundShooting != null) boundShooting.Fired += OnLocalShotFired;
                }
            }
        }

        private void PlaceLocalPlayerInSlot()
        {
            if (!isConnected) return;
            Transform local = FindLocalPlayer();
            if (local == null) return;
            CharacterController cc = local.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            local.position += new Vector3((myPlayerId - 1.5f) * 2.2f, 0f, 0f);
            Physics.SyncTransforms();
            if (cc != null) cc.enabled = true;
        }

        public static Transform FindLocalPlayer()
        {
            foreach (PlayerMovement movement in FindObjectsByType<PlayerMovement>())
            {
                if (movement != null && movement.GetComponent<CoopRemoteAvatar>() == null) return movement.transform;
            }
            return null;
        }

        // ====================================================================================================
        // INICIALIZAÇÃO DE HOST E CLIENT (UDP SOCKETS NATIVOS)
        // ====================================================================================================

        public void StartHost(int port)
        {
            StopNetwork();
            networkPort = port;
            currentRole = NetworkRole.Host;
            myPlayerId = 0;
            isConnected = false;
            lastPacketReceivedTime = Time.unscaledTime;

            lock (clientLock)
            {
                connectedClients.Clear();
            }
            remotePlayers.Clear();

            try
            {
                udpSocket = new UdpClient(networkPort);
                udpSocket.EnableBroadcast = true;
                DisableConnectionResetErrors();
                StartReceiveThread("ProjetoUV_Host_UDP_Thread");
                CoopSession.multiplayerConnectionLog = $"[HOST ONLINE - 4P] Sala aberta na porta {networkPort}! Aguardando até 3 jogadoras...";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjetoUVNetwork] Erro ao abrir Host na porta {networkPort}: {ex.Message}");
                CoopSession.multiplayerConnectionLog = $"[ERRO HOST] Falha ao abrir a porta {networkPort}: {ex.Message}";
                StopNetwork();
            }
        }

        public void StartClient(string targetIP, int port, string myNickname)
        {
            StopNetwork();
            hostAddress = targetIP.Trim();
            if (string.Equals(hostAddress, "localhost", StringComparison.OrdinalIgnoreCase)) hostAddress = "127.0.0.1";
            networkPort = port;
            currentRole = NetworkRole.Client;
            myPlayerId = -1; // aguarda o ID dado pelo Host (1, 2 ou 3)
            isConnected = false;
            lastPacketReceivedTime = Time.unscaledTime;
            remotePlayers.Clear();

            try
            {
                udpSocket = new UdpClient(0);
                hostEndPoint = new IPEndPoint(IPAddress.Parse(hostAddress), networkPort);
                DisableConnectionResetErrors();
                StartReceiveThread("ProjetoUV_Client_UDP_Thread");
                SendRawPacket($"HELLO|{myNickname}");
                CoopSession.multiplayerConnectionLog = $"[CONECTANDO] Enviando pedido para {hostAddress}:{networkPort}...";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjetoUVNetwork] Erro ao conectar em {hostAddress}:{networkPort}: {ex.Message}");
                CoopSession.multiplayerConnectionLog = $"[ERRO CLIENTE] Falha ao conectar: {ex.Message}";
                StopNetwork();
            }
        }

        private void DisableConnectionResetErrors()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                udpSocket.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch { }
#endif
        }

        private void StartReceiveThread(string threadName)
        {
            isRunning = true;
            receiveThread = new Thread(ReceiveThreadLoop) { IsBackground = true, Name = threadName };
            receiveThread.Start();
        }

        public void StopNetwork()
        {
            if (isRunning && isConnected && udpSocket != null)
            {
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes($"DISCONNECT|{myPlayerId}|{CoopSession.playerNickname}");
                    for (int i = 0; i < 4; i++)
                    {
                        if (currentRole == NetworkRole.Client && hostEndPoint != null)
                        {
                            udpSocket.Send(bytes, bytes.Length, hostEndPoint);
                        }
                        else if (currentRole == NetworkRole.Host)
                        {
                            lock (clientLock)
                            {
                                foreach (var kvp in connectedClients)
                                {
                                    if (kvp.Value != null) udpSocket.Send(bytes, bytes.Length, kvp.Value);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            isRunning = false;
            if (udpSocket != null)
            {
                try { udpSocket.Close(); } catch { }
                udpSocket = null;
            }
            receiveThread = null; // fechar o socket destrava o Receive e a thread termina sozinha

            lock (queueLock)
            {
                incomingPacketsQueue.Clear();
            }
            lock (clientLock)
            {
                connectedClients.Clear();
            }

            foreach (var slot in remotePlayers.Values)
            {
                if (slot != null && slot.avatar != null) Destroy(slot.avatar.gameObject);
            }
            remotePlayers.Clear();

            isConnected = false;
            currentRole = NetworkRole.Offline;
            myPlayerId = 0;
        }

        // ====================================================================================================
        // THREAD DE LEITURA UDP (sem chamar a API da Unity: só enfileira)
        // ====================================================================================================

        private void ReceiveThreadLoop()
        {
            IPEndPoint anySender = new IPEndPoint(IPAddress.Any, 0);
            while (isRunning && udpSocket != null)
            {
                try
                {
                    byte[] data = udpSocket.Receive(ref anySender);
                    if (data != null && data.Length > 0)
                    {
                        string message = Encoding.UTF8.GetString(data);
                        lock (queueLock)
                        {
                            incomingPacketsQueue.Enqueue(new IncomingPacket
                            {
                                message = message,
                                sender = new IPEndPoint(anySender.Address, anySender.Port)
                            });
                        }
                    }
                }
                catch (SocketException)
                {
                    if (!isRunning || udpSocket == null) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    if (!isRunning || udpSocket == null) break;
                }
            }
        }

        // ====================================================================================================
        // PROCESSAMENTO DE PACOTES NA MAIN THREAD
        // ====================================================================================================

        private void ProcessIncomingPacketsOnMainThread()
        {
            if (currentRole != NetworkRole.Offline && CoopSession.hasStartedRun) BindGame();

            while (true)
            {
                IncomingPacket packet = default;
                bool hasPacket = false;
                lock (queueLock)
                {
                    if (incomingPacketsQueue.Count > 0)
                    {
                        packet = incomingPacketsQueue.Dequeue();
                        hasPacket = true;
                    }
                }
                if (!hasPacket || string.IsNullOrEmpty(packet.message)) break;

                lastPacketReceivedTime = Time.unscaledTime;
                HandlePacket(packet.message, packet.sender);
            }
        }

        private void HandlePacket(string packet, IPEndPoint sender)
        {
            string[] parts = packet.Split('|');
            if (parts.Length == 0) return;

            switch (parts[0])
            {
                case "HELLO":
                    if (currentRole == NetworkRole.Host) HandleHello(parts, sender);
                    break;

                case "WELCOME":
                    if (currentRole == NetworkRole.Client) HandleWelcome(parts);
                    break;

                case "EXISTING_PEER":
                case "PEER_JOINED":
                    if (currentRole == NetworkRole.Client && parts.Length > 2 && int.TryParse(parts[1], out int peerId) && peerId != myPlayerId)
                    {
                        EnsureRemoteAvatarExists(peerId, parts[2]);
                        Subtitle($"[MULTIPLAYER 4P] '{parts[2]}' (Jogadora {peerId + 1}) juntou-se ao esquadrão!");
                    }
                    break;

                case "CLIENT_READY":
                    if (currentRole == NetworkRole.Host)
                    {
                        isConnected = true;
                        CoopSession.multiplayerConnectionLog = $"[SALA SINCRONIZADA] {ConnectedPeersCount + 1}/4 jogadoras prontas! Clique em 'INICIAR EXPEDIÇÃO'.";
                    }
                    break;

                case "START_GAME":
                    if (currentRole == NetworkRole.Client && !CoopSession.hasStartedRun)
                    {
                        CoopSession.customDungeonSeed = parts.Length > 1 ? parts[1] : "PROJETO-UV";
                        CoopSession.StartExpeditionFromNetwork();
                    }
                    break;

                case "PLYR":
                    HandleRemotePlayerStatePacket(parts, sender);
                    break;

                case "SHOT":
                    HandleRemoteShotPacket(parts, sender);
                    break;

                case "POST":
                    HandleRemotePostRepairedPacket(parts);
                    Relay(packet, sender);
                    break;

                case "FLOOR":
                    HandleRemoteFloorChangePacket(parts);
                    Relay(packet, sender);
                    break;

                case "ROOM_ENTER":
                    HandleRemoteRoomEnteredPacket(parts);
                    Relay(packet, sender);
                    break;

                case "ROOM_CLEARED":
                    HandleRemoteCombatClearedPacket(parts);
                    Relay(packet, sender);
                    break;

                case "DIED":
                    // só o Host sorteia a nova semente, para todas recomeçarem no mesmo mapa
                    if (currentRole == NetworkRole.Host) BeginRestartForAll($"{(parts.Length > 1 ? parts[1] : "Uma guardiã")} caiu na escuridão");
                    break;

                case "RESTART_RUN":
                    HandleRemoteRestartRunPacket(parts);
                    Relay(packet, sender);
                    break;

                case "DISCONNECT":
                    HandleDisconnect(parts, sender);
                    break;
            }
        }

        private void Relay(string packet, IPEndPoint sender)
        {
            if (currentRole == NetworkRole.Host) ForwardPacketToOtherClients(packet, sender);
        }

        private void HandleHello(string[] parts, IPEndPoint sender)
        {
            string clientNick = parts.Length > 1 ? parts[1] : "Guardiã";
            int assignedSlot = -1;

            lock (clientLock)
            {
                foreach (var kvp in connectedClients)
                {
                    if (kvp.Value.Equals(sender))
                    {
                        assignedSlot = kvp.Key;
                        break;
                    }
                }
                if (assignedSlot == -1)
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        if (!connectedClients.ContainsKey(i))
                        {
                            assignedSlot = i;
                            break;
                        }
                    }
                }
                if (assignedSlot != -1) connectedClients[assignedSlot] = sender;
            }

            if (assignedSlot == -1)
            {
                SendRawPacketTo("ROOM_FULL|A sala atingiu o limite de 4 jogadoras.", sender);
                return;
            }

            isConnected = true;
            RemotePlayerSlot joined = EnsureRemoteAvatarExists(assignedSlot, clientNick);
            joined.lastPacketTime = Time.unscaledTime;

            if (string.IsNullOrWhiteSpace(CoopSession.customDungeonSeed)) CoopSession.customDungeonSeed = CoopSession.NewSeed();
            SendRawPacketTo($"WELCOME|{assignedSlot}|{CoopSession.customDungeonSeed}|{CoopSession.playerNickname}", sender);

            lock (clientLock)
            {
                foreach (var kvp in connectedClients)
                {
                    if (kvp.Key != assignedSlot && remotePlayers.TryGetValue(kvp.Key, out var existingSlot))
                    {
                        SendRawPacketTo($"EXISTING_PEER|{kvp.Key}|{existingSlot.nickname}", sender);
                    }
                }
            }
            ForwardPacketToOtherClients($"PEER_JOINED|{assignedSlot}|{clientNick}", sender);

            if (CoopSession.hasStartedRun) SendRawPacketTo($"START_GAME|{CoopSession.customDungeonSeed}", sender);

            CoopSession.multiplayerConnectionLog = $"[CONECTADO!] {ConnectedPeersCount + 1}/4 jogadoras na sala ({clientNick} entrou)!";
            Subtitle($"[MULTIPLAYER 4P] '{clientNick}' entrou no esquadrão ({ConnectedPeersCount + 1}/4 jogadoras)!");
        }

        private void HandleWelcome(string[] parts)
        {
            myPlayerId = parts.Length > 1 && int.TryParse(parts[1], out int myAssignedId) ? myAssignedId : 1;
            string seed = parts.Length > 2 ? parts[2] : "PROJETO-UV";
            string hostNick = parts.Length > 3 ? parts[3] : "Host";

            isConnected = true;
            EnsureRemoteAvatarExists(0, hostNick).lastPacketTime = Time.unscaledTime;
            CoopSession.customDungeonSeed = seed;
            CoopSession.multiplayerConnectionLog = $"[CONECTADA AO HOST: {hostNick}!] Você é a Jogadora {myPlayerId + 1}. Aguardando o Host iniciar a expedição...";
            SendRawPacket($"CLIENT_READY|{myPlayerId}|{CoopSession.playerNickname}");
        }

        private void HandleDisconnect(string[] parts, IPEndPoint sender)
        {
            string quitter = parts.Length > 2 ? parts[2] : (parts.Length > 1 ? parts[1] : "Uma jogadora");
            int quitterId = 0;
            if (parts.Length > 1) int.TryParse(parts[1], out quitterId);

            if (currentRole == NetworkRole.Client)
            {
                if (quitterId == 0)
                {
                    HandlePeerDisconnected($"O Host ('{quitter}') encerrou a partida.");
                }
                else
                {
                    RemoveRemotePlayerAvatar(quitterId);
                    Subtitle($"[MULTIPLAYER 4P] '{quitter}' saiu da sala.");
                }
            }
            else if (currentRole == NetworkRole.Host)
            {
                lock (clientLock)
                {
                    connectedClients.Remove(quitterId);
                }
                RemoveRemotePlayerAvatar(quitterId);
                ForwardPacketToOtherClients($"DISCONNECT|{quitterId}|{quitter}", sender);
                CoopSession.multiplayerConnectionLog = $"[JOGADORA SAIU] '{quitter}' saiu da sala. Restam {ConnectedPeersCount + 1}/4.";
                Subtitle($"[MULTIPLAYER 4P] '{quitter}' saiu da sala.");
            }
        }

        // ====================================================================================================
        // ESTADO DA JOGADORA LOCAL
        // ====================================================================================================

        private void SendLocalPlayerState()
        {
            if (!CoopSession.hasStartedRun) return;
            Transform local = FindLocalPlayer();
            if (local == null) return;

            PlayerHealth health = local.GetComponent<PlayerHealth>();
            PlayerMana mana = local.GetComponent<PlayerMana>();
            PlayerShooting shooting = local.GetComponent<PlayerShooting>();
            PlayerMovement movement = local.GetComponent<PlayerMovement>();

            Vector3 pos = local.position;
            string packet = string.Format(
                CultureInfo.InvariantCulture,
                "PLYR|{0}|{1:F2}|{2:F2}|{3:F2}|{4:F1}|{5:F1}|{6:F1}|{7:F1}|{8:F1}|{9}|{10}|{11}|{12}",
                myPlayerId,
                pos.x, pos.y, pos.z,
                local.eulerAngles.y,
                health != null ? health.Health : 0f,
                health != null ? health.MaxHealth : 100f,
                mana != null ? mana.Mana : 0f,
                mana != null ? mana.MaxMana : 100f,
                health != null && health.IsDead ? 1 : 0,
                shooting != null ? (int)shooting.CurrentWeapon : 0,
                movement != null && movement.IsMoving ? 1 : 0,
                boundMap != null ? boundMap.currentFloorIndex : 0
            );
            SendRawPacket(packet);
        }

        private void HandleRemotePlayerStatePacket(string[] parts, IPEndPoint sender)
        {
            if (parts.Length < 14 || !int.TryParse(parts[1], out int pid) || pid == myPlayerId) return;

            if (TryFloat(parts[2], out float px) && TryFloat(parts[3], out float py) && TryFloat(parts[4], out float pz) && TryFloat(parts[5], out float ry))
            {
                var slot = EnsureRemoteAvatarExists(pid, $"Guardiã {pid + 1}");
                slot.targetPosition = new Vector3(px, py, pz);
                slot.targetRotation = Quaternion.Euler(0f, ry, 0f);
                slot.lastPacketTime = Time.unscaledTime;

                if (slot.avatar != null)
                {
                    if (!slot.hasReceivedFirstPosition)
                    {
                        slot.avatar.transform.SetPositionAndRotation(slot.targetPosition, slot.targetRotation);
                        slot.hasReceivedFirstPosition = true;
                    }
                    TryFloat(parts[6], out slot.avatar.health);
                    TryFloat(parts[7], out slot.avatar.maxHealth);
                    TryFloat(parts[8], out slot.avatar.mana);
                    TryFloat(parts[9], out slot.avatar.maxMana);
                    slot.avatar.isDead = parts[10] == "1";
                    if (int.TryParse(parts[11], out int weapon)) slot.avatar.weapon = (WeaponType)weapon;
                    slot.avatar.isMoving = parts[12] == "1";
                    if (int.TryParse(parts[13], out int floor)) slot.avatar.floorIndex = floor;
                }
            }

            if (currentRole == NetworkRole.Host) ForwardPacketToOtherClients(string.Join("|", parts), sender);
        }

        private void InterpolateRemoteAvatarTransform()
        {
            foreach (var slot in remotePlayers.Values)
            {
                if (slot == null || slot.avatar == null || !slot.hasReceivedFirstPosition) continue;

                Transform tr = slot.avatar.transform;
                bool sameFloor = boundMap == null || slot.avatar.floorIndex == boundMap.currentFloorIndex;
                slot.avatar.SetVisible(sameFloor);
                if (Vector3.Distance(tr.position, slot.targetPosition) > 6.0f)
                {
                    tr.position = slot.targetPosition;
                }
                else
                {
                    tr.position = Vector3.Lerp(tr.position, slot.targetPosition, Time.unscaledDeltaTime * 22f);
                }
                tr.rotation = Quaternion.Slerp(tr.rotation, slot.targetRotation, Time.unscaledDeltaTime * 22f);
            }
        }

        // ====================================================================================================
        // TIROS PELA REDE
        // ====================================================================================================

        private void OnLocalShotFired(WeaponType weapon)
        {
            if (!isConnected || boundShooting == null || boundShooting.Muzzle == null) return;
            Transform muzzle = boundShooting.Muzzle;
            Vector3 origin = muzzle.position;
            Vector3 forward = muzzle.forward;
            string packet = string.Format(
                CultureInfo.InvariantCulture,
                "SHOT|{0}|{1:F2}|{2:F2}|{3:F2}|{4:F3}|{5:F3}|{6:F3}|{7}",
                myPlayerId, origin.x, origin.y, origin.z, forward.x, forward.y, forward.z, (int)weapon);
            SendRawPacket(packet);
        }

        private void HandleRemoteShotPacket(string[] parts, IPEndPoint sender)
        {
            if (parts.Length < 9 || !int.TryParse(parts[1], out int pid) || pid == myPlayerId) return;

            if (TryFloat(parts[2], out float ox) && TryFloat(parts[3], out float oy) && TryFloat(parts[4], out float oz) &&
                TryFloat(parts[5], out float dx) && TryFloat(parts[6], out float dy) && TryFloat(parts[7], out float dz) &&
                int.TryParse(parts[8], out int weapon))
            {
                var slot = EnsureRemoteAvatarExists(pid, $"Guardiã {pid + 1}");
                if (slot.avatar != null && slot.avatar.IsVisible)
                {
                    slot.avatar.ReplayShot(new Vector3(ox, oy, oz), new Vector3(dx, dy, dz), (WeaponType)weapon, boundShooting);
                }
            }

            if (currentRole == NetworkRole.Host) ForwardPacketToOtherClients(string.Join("|", parts), sender);
        }

        // ====================================================================================================
        // POSTES, SALAS E ANDARES
        // ====================================================================================================

        private void OnLocalPostRepaired(GameObject post)
        {
            if (!isConnected || applyingRemote || post == null) return;
            Vector3 pos = post.transform.position;
            string packet = string.Format(CultureInfo.InvariantCulture, "POST|{0:F2}|{1:F2}|{2:F2}", pos.x, pos.y, pos.z);
            for (int i = 0; i < 3; i++) SendRawPacket(packet);
        }

        private void HandleRemotePostRepairedPacket(string[] parts)
        {
            if (parts.Length < 4 || !TryFloat(parts[1], out float px) || !TryFloat(parts[2], out float py) || !TryFloat(parts[3], out float pz)) return;

            Vector3 targetPos = new Vector3(px, py, pz);
            foreach (var p in FindObjectsByType<StandaloneSmartPost>(FindObjectsInactive.Include))
            {
                if (p != null && !p.isRepaired && Vector3.Distance(p.transform.position, targetPos) < 2.5f)
                {
                    applyingRemote = true;
                    try { p.ForceRepair(); }
                    finally { applyingRemote = false; }
                    break;
                }
            }
        }

        private void OnLocalFloorGenerated(int floorIndex, int level, string title)
        {
            PlaceLocalPlayerInSlot();
            if (applyingRemote || floorIndex <= lastSyncedFloor) return;
            lastSyncedFloor = floorIndex;
            if (isConnected) SendRawPacket($"FLOOR|{floorIndex}");
        }

        private void HandleRemoteFloorChangePacket(string[] parts)
        {
            if (parts.Length < 2 || !int.TryParse(parts[1], out int floorIdx) || boundMap == null) return;
            if (floorIdx <= boundMap.currentFloorIndex) return;

            lastSyncedFloor = floorIdx;
            applyingRemote = true;
            try { boundMap.GenerateFloor(floorIdx); }
            finally { applyingRemote = false; }
            Subtitle("[CO-OP] Uma parceira subiu a escadaria: o esquadrão inteiro foi para o próximo andar!");
        }

        private void OnLocalRoomEntered(Vector2Int gridCoord, string roomType)
        {
            if (!isConnected || applyingRemote) return;
            Transform local = FindLocalPlayer();
            if (local == null) return;
            Vector3 leaderPos = local.position;
            string packet = string.Format(CultureInfo.InvariantCulture, "ROOM_ENTER|{0}|{1}|{2:F2}|{3:F2}|{4:F2}|{5}",
                gridCoord.x, gridCoord.y, leaderPos.x, leaderPos.y, leaderPos.z, boundMap != null ? boundMap.currentFloorIndex : 0);
            for (int i = 0; i < 3; i++) SendRawPacket(packet);
        }

        private void HandleRemoteRoomEnteredPacket(string[] parts)
        {
            if (parts.Length < 7 || boundMap == null) return;
            if (!int.TryParse(parts[1], out int gx) || !int.TryParse(parts[2], out int gy) ||
                !TryFloat(parts[3], out float lx) || !TryFloat(parts[4], out float ly) || !TryFloat(parts[5], out float lz) ||
                !int.TryParse(parts[6], out int floor) || floor != boundMap.currentFloorIndex)
            {
                return;
            }

            Vector2Int coord = new Vector2Int(gx, gy);
            if (!boundMap.DungeonGrid.TryGetValue(coord, out var targetRoom)) return;
            if (boundMap.currentPlayerRoomCoord == coord || (targetRoom.mergedGiantPartner != null && targetRoom.mergedGiantPartner.gridCoord == boundMap.currentPlayerRoomCoord)) return;

            Transform local = FindLocalPlayer();
            if (local == null) return;
            PlayerHealth health = local.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead) return;

            // entra 3,5 m na sala a partir de onde a parceira passou, ao lado dela
            Vector3 leaderPos = new Vector3(lx, ly, lz);
            Vector3 toCenter = targetRoom.worldCenter - leaderPos;
            toCenter.y = 0f;
            toCenter = toCenter.sqrMagnitude < 0.001f ? Vector3.forward : toCenter.normalized;
            Vector3 rightPerp = new Vector3(-toCenter.z, 0f, toCenter.x);
            Vector3 spot = leaderPos + toCenter * 3.5f + rightPerp * ((myPlayerId - 1.5f) * 1.5f);
            float halfW = boundMap.roomWidthX * 0.5f - 3f;
            float halfL = boundMap.roomLengthZ * 0.5f - 3f;
            spot.x = Mathf.Clamp(spot.x, targetRoom.worldCenter.x - halfW, targetRoom.worldCenter.x + halfW);
            spot.z = Mathf.Clamp(spot.z, targetRoom.worldCenter.z - halfL, targetRoom.worldCenter.z + halfL);
            spot.y = local.position.y;

            CharacterController cc = local.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            local.position = spot;
            Physics.SyncTransforms();
            if (cc != null) cc.enabled = true;

            Subtitle("[CO-OP REAGRUPAR] Uma parceira entrou numa nova sala e o esquadrão foi reunido!");
        }

        // Quando a sala atual abre as portas (vencida aqui), avisa as outras.
        private void WatchCurrentRoomCleared()
        {
            if (boundMap == null || !CoopSession.hasStartedRun) return;
            if (!boundMap.DungeonGrid.TryGetValue(boundMap.currentPlayerRoomCoord, out var room)) return;

            if (room != watchedRoom)
            {
                watchedRoom = room;
                watchedRoomWasOpen = room.doorsUnlocked;
                return;
            }
            if (!watchedRoomWasOpen && room.doorsUnlocked)
            {
                watchedRoomWasOpen = true;
                bool combat = room.roomType == StandaloneLighthouseMapSystem.RoomType.Combat
                              || room.roomType == StandaloneLighthouseMapSystem.RoomType.GiantHall
                              || room.roomType == StandaloneLighthouseMapSystem.RoomType.BossExit;
                if (combat && !applyingRemote)
                {
                    string packet = $"ROOM_CLEARED|{room.gridCoord.x}|{room.gridCoord.y}|{boundMap.currentFloorIndex}";
                    for (int i = 0; i < 3; i++) SendRawPacket(packet);
                }
            }
        }

        private void HandleRemoteCombatClearedPacket(string[] parts)
        {
            if (parts.Length < 4 || boundMap == null) return;
            if (!int.TryParse(parts[1], out int gx) || !int.TryParse(parts[2], out int gy) ||
                !int.TryParse(parts[3], out int floor) || floor != boundMap.currentFloorIndex)
            {
                return;
            }
            if (!boundMap.DungeonGrid.TryGetValue(new Vector2Int(gx, gy), out var room) || room.doorsUnlocked) return;

            applyingRemote = true;
            try
            {
                if (MonsterDirector.Instance != null) MonsterDirector.Instance.EndRoom(room);
                DefeatRoomMonsters(room);
                if (room.mergedGiantPartner != null) DefeatRoomMonsters(room.mergedGiantPartner);
                foreach (var post in room.roomPosts)
                {
                    if (post != null) post.ForceRepair();
                }
                if (room.mergedGiantPartner != null)
                {
                    foreach (var post in room.mergedGiantPartner.roomPosts)
                    {
                        if (post != null) post.ForceRepair();
                    }
                }
                room.monsterQuota = room.monstersSpawned;
                if (room.mergedGiantPartner != null) room.mergedGiantPartner.monsterQuota = room.mergedGiantPartner.monstersSpawned;
            }
            finally
            {
                applyingRemote = false;
            }
            Subtitle("[CO-OP] Uma parceira venceu a sala! Os monstros restantes foram dissipados.");
        }

        private static void DefeatRoomMonsters(StandaloneLighthouseMapSystem.MapRoomNode room)
        {
            foreach (GameObject m in room.roomMonsters.ToArray())
            {
                if (m == null) continue;
                Roguelike.Monster monster = m.GetComponent<Roguelike.Monster>();
                if (monster != null && monster.Health != null)
                {
                    monster.Health.Invulnerable = false;
                    monster.Health.TakeDamage(999999f, m.transform.position);
                    continue;
                }
                ShadowEnemy shadow = m.GetComponent<ShadowEnemy>();
                if (shadow != null)
                {
                    shadow.TakeDamage(999999f);
                    continue;
                }
                Destroy(m);
            }
        }

        // ====================================================================================================
        // MORTE E RECOMEÇO DA EXPEDIÇÃO (ROGUELIKE)
        // ====================================================================================================

        private void OnLocalPlayerDied()
        {
            if (!isConnected) return;
            if (currentRole == NetworkRole.Host)
            {
                BeginRestartForAll($"{CoopSession.playerNickname} caiu na escuridão");
            }
            else
            {
                for (int i = 0; i < 3; i++) SendRawPacket($"DIED|{CoopSession.playerNickname}");
            }
        }

        /// <summary>Host: recomeça a expedição para todas (F5 / "Jogar novamente").</summary>
        public void RestartRunForAll(string who)
        {
            if (currentRole == NetworkRole.Host) BeginRestartForAll($"{who} reiniciou a expedição");
        }

        // Host: nova semente para a nova expedição, avisando todas.
        private void BeginRestartForAll(string reason)
        {
            CoopSession.customDungeonSeed = CoopSession.NewSeed();
            string packet = $"RESTART_RUN|{CoopSession.customDungeonSeed}|{reason}";
            for (int i = 0; i < 3; i++) SendRawPacket(packet);
            ApplyRestart(reason);
        }

        private void HandleRemoteRestartRunPacket(string[] parts)
        {
            if (parts.Length < 2) return;
            if (parts[1] == CoopSession.customDungeonSeed && pendingRestartTime > 0f) return;
            CoopSession.customDungeonSeed = parts[1];
            ApplyRestart(parts.Length > 2 ? parts[2] : "Uma guardiã caiu na escuridão");
        }

        // A jogadora morta recomeça sozinha (PlayerHealth recarrega a cena); as vivas recarregam logo depois do aviso.
        private void ApplyRestart(string reason)
        {
            Subtitle($"[CO-OP] {reason}! A expedição recomeça do Andar 1-1 num farol novo...");
            Transform local = FindLocalPlayer();
            PlayerHealth health = local != null ? local.GetComponent<PlayerHealth>() : null;
            if (health == null || !health.IsDead)
            {
                pendingRestartTime = Time.unscaledTime + 2.5f;
            }
        }

        public void HandlePeerDisconnected(string reason)
        {
            if (!isConnected && currentRole == NetworkRole.Offline) return;
            bool wasInRun = CoopSession.hasStartedRun;
            StopNetwork();
            CoopSession.multiplayerConnectionLog = $"[DESCONECTADA] {reason}";
            CoopSession.ResetToSingleplayer();
            Subtitle($"[MULTIPLAYER] {reason} Continuando sozinha.");
            if (wasInRun && MultiplayerAndAccessibilityHUD.Instance != null)
            {
                MultiplayerAndAccessibilityHUD.Instance.SetSubtitle($"[MULTIPLAYER] {reason} A partida continua no modo solo.");
            }
        }

        // ====================================================================================================
        // AVATARES REMOTOS (ATÉ 4 JOGADORAS)
        // ====================================================================================================

        public RemotePlayerSlot EnsureRemoteAvatarExists(int playerId, string nickname)
        {
            if (!remotePlayers.TryGetValue(playerId, out var slot) || slot == null)
            {
                slot = new RemotePlayerSlot
                {
                    playerId = playerId,
                    nickname = nickname,
                    lastPacketTime = Time.unscaledTime
                };
                remotePlayers[playerId] = slot;
            }
            if (!nickname.StartsWith("Guardiã ")) slot.nickname = nickname;

            // o avatar só existe dentro do jogo (é recriado a cada carregamento da cena)
            if (slot.avatar == null && CoopSession.hasStartedRun && FindLocalPlayer() != null)
            {
                Color color = PlayerSlotColors[Mathf.Clamp(playerId, 0, PlayerSlotColors.Length - 1)];
                slot.avatar = CoopRemoteAvatar.Create(playerId, slot.nickname, color, FindLocalPlayer());
                slot.hasReceivedFirstPosition = false;
            }
            else if (slot.avatar != null)
            {
                slot.avatar.nickname = slot.nickname;
            }
            return slot;
        }

        private void RemoveRemotePlayerAvatar(int pid)
        {
            if (remotePlayers.TryGetValue(pid, out var slot))
            {
                if (slot != null && slot.avatar != null) Destroy(slot.avatar.gameObject);
                remotePlayers.Remove(pid);
            }
        }

        public IEnumerable<CoopRemoteAvatar> RemoteAvatars()
        {
            foreach (var slot in remotePlayers.Values)
            {
                if (slot != null && slot.avatar != null) yield return slot.avatar;
            }
        }

        // ====================================================================================================
        // ENVIO DE PACOTES
        // ====================================================================================================

        public void NotifyStartRun(string seed)
        {
            if (!isConnected || currentRole != NetworkRole.Host) return;
            for (int i = 0; i < 4; i++) SendRawPacket($"START_GAME|{seed}");
        }

        private static void Subtitle(string message)
        {
            if (MultiplayerAndAccessibilityHUD.Instance != null) MultiplayerAndAccessibilityHUD.Instance.SetSubtitle(message);
            else if (StandaloneLighthouseMapSystem.Instance != null) StandaloneLighthouseMapSystem.Instance.ShowBannerMessage(message);
        }

        private static bool TryFloat(string s, out float value) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private void SendRawPacket(string message)
        {
            if (udpSocket == null) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                if (currentRole == NetworkRole.Host)
                {
                    lock (clientLock)
                    {
                        foreach (var kvp in connectedClients)
                        {
                            if (kvp.Value != null) udpSocket.Send(bytes, bytes.Length, kvp.Value);
                        }
                    }
                }
                else if (currentRole == NetworkRole.Client && hostEndPoint != null)
                {
                    udpSocket.Send(bytes, bytes.Length, hostEndPoint);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProjetoUVNetwork] Falha ao enviar pacote '{message}': {ex.Message}");
            }
        }

        private void SendRawPacketTo(string message, IPEndPoint target)
        {
            if (udpSocket == null || target == null) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                udpSocket.Send(bytes, bytes.Length, target);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProjetoUVNetwork] Falha ao enviar pacote direto '{message}': {ex.Message}");
            }
        }

        private void ForwardPacketToOtherClients(string packet, IPEndPoint excludeSender)
        {
            if (udpSocket == null || currentRole != NetworkRole.Host) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(packet);
                lock (clientLock)
                {
                    foreach (var kvp in connectedClients)
                    {
                        if (kvp.Value != null && !kvp.Value.Equals(excludeSender)) udpSocket.Send(bytes, bytes.Length, kvp.Value);
                    }
                }
            }
            catch { }
        }
    }
}
