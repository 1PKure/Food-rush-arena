using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    public static FusionLauncher Instance { get; private set; }

    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private string raceSceneName = "RaceScene";

    [Header("Room Settings")]
    [SerializeField] private int maxPlayers = 2;

    [Header("Prefabs")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Header("UI")]
    [SerializeField] private LobbyUI lobbyUI;

    private NetworkRunner runner;
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    private bool isReturningToLobby;
    private bool isStartingRoom;

    public NetworkRunner Runner => runner;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        TryFindLobbyUI();

        if (lobbyUI != null)
        {
            lobbyUI.ShowLobbyState();
        }

        await CreateLobbyRunner();
    }

    private void TryFindLobbyUI()
    {
        if (lobbyUI != null)
        {
            return;
        }

        lobbyUI = FindFirstObjectByType<LobbyUI>();
    }

    private async Task CreateLobbyRunner()
    {
        await DestroyCurrentRunner();

        TryFindLobbyUI();

        GameObject runnerObject = new GameObject("LobbyRunner");
        DontDestroyOnLoad(runnerObject);

        runner = runnerObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = false;
        runner.AddCallbacks(this);

        StartGameResult result = await runner.JoinSessionLobby(SessionLobby.ClientServer);

        if (!result.Ok)
        {
            Debug.LogError($"Failed to join client-server lobby: {result.ShutdownReason}");
            return;
        }

        Debug.Log("Joined client-server lobby successfully.");
    }

    private async Task CreateGameRunnerAndStart(string roomName)
    {
        await DestroyCurrentRunner();

        int raceSceneIndex = GetSceneBuildIndex(raceSceneName);

        if (raceSceneIndex < 0)
        {
            Debug.LogError($"Race scene '{raceSceneName}' was not found in Build Settings.");
            return;
        }

        GameObject runnerObject = new GameObject("GameRunner");
        DontDestroyOnLoad(runnerObject);

        runner = runnerObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        NetworkSceneManagerDefault sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(raceSceneIndex));

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = roomName,
            Scene = sceneInfo,
            SceneManager = sceneManager,
            PlayerCount = maxPlayers
        });

        if (!result.Ok)
        {
            Debug.LogError($"Failed to start/join room: {result.ShutdownReason}");

            await CreateLobbyRunner();

            TryFindLobbyUI();

            if (lobbyUI != null)
            {
                lobbyUI.ShowLobbyState();
            }

            isStartingRoom = false;
            return;
        }

        Debug.Log($"Connected to room: {roomName} | GameMode: {runner.GameMode} | IsServer: {runner.IsServer}");
    }

    private async Task DestroyCurrentRunner()
    {
        if (runner == null)
        {
            return;
        }

        runner.RemoveCallbacks(this);

        if (runner.IsRunning)
        {
            await runner.Shutdown();
        }

        spawnedPlayers.Clear();

        if (runner != null)
        {
            Destroy(runner.gameObject);
            runner = null;
        }
    }

    private int GetSceneBuildIndex(string sceneName)
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string currentSceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            if (currentSceneName == sceneName)
            {
                return i;
            }
        }

        return -1;
    }

    public async void CreateRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("Room name is empty.");
            return;
        }

        if (isStartingRoom)
        {
            return;
        }

        isStartingRoom = true;
        await CreateGameRunnerAndStart(roomName);
        isStartingRoom = false;
    }

    public async void JoinRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("Room name is empty.");
            return;
        }

        if (isStartingRoom)
        {
            return;
        }

        isStartingRoom = true;
        await CreateGameRunnerAndStart(roomName);
        isStartingRoom = false;
    }

    public async void LeaveRoom()
    {
        if (isReturningToLobby)
        {
            return;
        }

        isReturningToLobby = true;

        Debug.Log("[Fusion] Leaving room...");

        await DestroyCurrentRunner();

        if (SceneManager.GetActiveScene().name != lobbySceneName)
        {
            SceneManager.LoadScene(lobbySceneName);
            await Task.Yield();
        }

        TryFindLobbyUI();

        if (lobbyUI != null)
        {
            lobbyUI.ShowLobbyState();
        }

        await CreateLobbyRunner();

        isReturningToLobby = false;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Fusion] Player joined: {player} | IsServer: {runner.IsServer}");

        if (!runner.IsServer)
        {
            return;
        }

        if (spawnedPlayers.ContainsKey(player))
        {
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(player);

        NetworkObject playerObject = runner.Spawn(
            playerPrefab,
            spawnPosition,
            Quaternion.identity,
            player
        );

        spawnedPlayers[player] = playerObject;

        Debug.Log($"[Fusion] Spawned player object for {player} at {spawnPosition}");
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Fusion] Player left: {player} | IsServer: {runner.IsServer}");

        if (!runner.IsServer)
        {
            return;
        }

        if (spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
        {
            if (playerObject != null)
            {
                runner.Despawn(playerObject);
            }

            spawnedPlayers.Remove(player);
        }
    }

    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        int index = Mathf.Abs(player.RawEncoded) % maxPlayers;
        float spacing = 2.5f;

        return new Vector3(index * spacing, 0.5f, 0f);
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"Session list updated. Rooms found: {sessionList.Count}");

        TryFindLobbyUI();

        if (lobbyUI != null)
        {
            lobbyUI.RefreshRoomList(sessionList);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerInputData data = new PlayerInputData();

        Vector2 moveInput = Vector2.zero;

        if (Input.GetKey(KeyCode.W)) moveInput.y += 1f;
        if (Input.GetKey(KeyCode.S)) moveInput.y -= 1f;
        if (Input.GetKey(KeyCode.A)) moveInput.x -= 1f;
        if (Input.GetKey(KeyCode.D)) moveInput.x += 1f;

        data.Move = moveInput.normalized;
        input.Set(data);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[Fusion] Shutdown: {shutdownReason}");
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[Fusion] Connected to server.");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[Fusion] Disconnected: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[Fusion] Connect failed: {reason}");
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[Fusion] Scene load done.");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("[Fusion] Scene load started.");
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}