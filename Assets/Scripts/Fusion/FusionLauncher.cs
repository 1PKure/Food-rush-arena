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
        if (lobbyUI != null)
        {
            lobbyUI.ShowLobbyState();
        }

        await CreateLobbyRunner();
    }

    private async Task CreateLobbyRunner()
    {
        await DestroyCurrentRunner();

        GameObject runnerObject = new GameObject("LobbyRunner");
        DontDestroyOnLoad(runnerObject);

        runner = runnerObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = false;
        runner.AddCallbacks(this);

        var result = await runner.JoinSessionLobby(SessionLobby.Shared);

        if (!result.Ok)
        {
            Debug.LogError($"Failed to join shared lobby: {result.ShutdownReason}");
            return;
        }

        Debug.Log("Joined shared lobby successfully.");
    }

    private async Task CreateGameRunnerAndStart(string roomName)
    {
        await DestroyCurrentRunner();

        GameObject runnerObject = new GameObject("GameRunner");
        DontDestroyOnLoad(runnerObject);

        runner = runnerObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = roomName,
            Scene = sceneInfo,
            PlayerCount = 8
        });

        if (!result.Ok)
        {
            Debug.LogError($"Failed to start/join room: {result.ShutdownReason}");
            await CreateLobbyRunner();
            if (lobbyUI != null)
            {
                lobbyUI.ShowLobbyState();
            }
            return;
        }

        Debug.Log($"Connected to room: {roomName}");

        if (lobbyUI != null)
        {
            lobbyUI.ShowInRoomState(roomName);
        }
    }

    private async Task DestroyCurrentRunner()
    {
        if (runner == null)
            return;

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

    public async void CreateRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("Room name is empty.");
            return;
        }

        if (isStartingRoom)
            return;

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
            return;

        isStartingRoom = true;
        await CreateGameRunnerAndStart(roomName);
        isStartingRoom = false;
    }

    public async void LeaveRoom()
    {
        if (isReturningToLobby)
            return;

        isReturningToLobby = true;

        Debug.Log("[Fusion] Left Room");

        if (lobbyUI != null)
        {
            lobbyUI.ShowLobbyState();
        }

        await CreateLobbyRunner();

        isReturningToLobby = false;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player joined: {player}");

        if (player == runner.LocalPlayer)
        {
            Vector3 spawnPosition = GetSpawnPosition(player);

            NetworkObject playerObject = runner.Spawn(
                playerPrefab,
                spawnPosition,
                Quaternion.identity,
                player
            );

            spawnedPlayers[player] = playerObject;
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player left: {player}");

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
        int index = player.RawEncoded % 8;
        float spacing = 2.5f;

        return new Vector3(index * spacing, 0.5f, 0f);
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"Session list updated. Rooms found: {sessionList.Count}");
        lobbyUI?.RefreshRoomList(sessionList);
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

    public void OnConnectedToServer(NetworkRunner runner) { }

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

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}