using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace RustSurvival.Networking
{
    /// <summary>
    /// Host/client session flow using Lobby for discovery and Relay for connectivity.
    /// The host is still a player-operated authority, not a dedicated server.
    /// </summary>
    public sealed class NetworkSessionManager : MonoBehaviour
    {
        private const string RelayJoinCodeKey = "relayJoinCode";

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport unityTransport;
        [SerializeField, Range(2, 16)] private int maxPlayers = 8;
        [SerializeField] private string relayConnectionType = "dtls";

        private Lobby _currentLobby;
        private bool _isLobbyHost;
        private Coroutine _heartbeatRoutine;
        private bool _servicesReady;

        public event Action<string> StatusChanged;
        public string LastStatus { get; private set; } = "Desconectado";
        public string CurrentLobbyCode => _currentLobby != null ? _currentLobby.LobbyCode : string.Empty;

        private void Awake()
        {
            if (networkManager == null) networkManager = NetworkManager.Singleton;
            if (unityTransport == null && networkManager != null)
                unityTransport = networkManager.GetComponent<UnityTransport>();
        }

        public async Task<string> CreateLobbyAndHostAsync(string lobbyName)
        {
            try
            {
                await EnsureServicesAsync();
                EnsureNetworkReferences();

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                var options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                    }
                };

                _currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                    string.IsNullOrWhiteSpace(lobbyName) ? "Rust Survival" : lobbyName,
                    maxPlayers,
                    options);

                ConfigureRelayTransport(allocation);
                if (!networkManager.StartHost())
                    throw new InvalidOperationException("NGO no pudo iniciar como host.");

                _isLobbyHost = true;
                _heartbeatRoutine = StartCoroutine(HeartbeatLoop());
                SetStatus("Sala creada. Código: " + _currentLobby.LobbyCode);
                return _currentLobby.LobbyCode;
            }
            catch (Exception exception)
            {
                SetStatus("Error al crear sala: " + exception.Message);
                throw;
            }
        }

        public async Task JoinLobbyAsync(string lobbyCode)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
                throw new ArgumentException("Introduce el código de la sala.", nameof(lobbyCode));

            try
            {
                await EnsureServicesAsync();
                EnsureNetworkReferences();

                _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim());
                if (_currentLobby.Data == null ||
                    !_currentLobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayData) ||
                    string.IsNullOrWhiteSpace(relayData.Value))
                    throw new InvalidOperationException("La sala no contiene un código Relay válido.");

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayData.Value);
                ConfigureRelayTransport(allocation);

                if (!networkManager.StartClient())
                    throw new InvalidOperationException("NGO no pudo iniciar como cliente.");

                _isLobbyHost = false;
                SetStatus("Conectado a la sala " + _currentLobby.Name);
            }
            catch (Exception exception)
            {
                SetStatus("Error al entrar: " + exception.Message);
                throw;
            }
        }

        public async Task LeaveLobbyAsync()
        {
            if (_heartbeatRoutine != null)
            {
                StopCoroutine(_heartbeatRoutine);
                _heartbeatRoutine = null;
            }

            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();

            if (_currentLobby != null && _servicesReady)
            {
                try
                {
                    if (_isLobbyHost)
                        await LobbyService.Instance.DeleteLobbyAsync(_currentLobby.Id);
                    else if (!string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId))
                        await LobbyService.Instance.RemovePlayerAsync(_currentLobby.Id, AuthenticationService.Instance.PlayerId);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Lobby cleanup: " + exception.Message);
                }
            }

            _currentLobby = null;
            _isLobbyHost = false;
            SetStatus("Desconectado");
        }

        private void ConfigureRelayTransport(Allocation allocation)
        {
            bool isWebPlatform = Application.platform == RuntimePlatform.WebGLPlayer;
            string connectionType = isWebPlatform ? "wss" : relayConnectionType;
            unityTransport.UseWebSockets = isWebPlatform;
            unityTransport.SetRelayServerData(new RelayServerData(allocation, connectionType));
        }

        private void ConfigureRelayTransport(JoinAllocation allocation)
        {
            bool isWebPlatform = Application.platform == RuntimePlatform.WebGLPlayer;
            string connectionType = isWebPlatform ? "wss" : relayConnectionType;
            unityTransport.UseWebSockets = isWebPlatform;
            unityTransport.SetRelayServerData(new RelayServerData(allocation, connectionType));
        }

        private async Task EnsureServicesAsync()
        {
            if (!_servicesReady)
            {
                await UnityServices.InitializeAsync();
                _servicesReady = true;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private void EnsureNetworkReferences()
        {
            if (networkManager == null) networkManager = NetworkManager.Singleton;
            if (networkManager == null)
                throw new InvalidOperationException("Añade un NetworkManager a la escena.");

            if (unityTransport == null)
                unityTransport = networkManager.GetComponent<UnityTransport>();
            if (unityTransport == null)
                throw new InvalidOperationException("Añade UnityTransport al NetworkManager.");
        }

        private IEnumerator HeartbeatLoop()
        {
            var wait = new WaitForSecondsRealtime(15f);
            while (_currentLobby != null && _isLobbyHost)
            {
                yield return wait;
                _ = SendHeartbeatAsync();
            }
        }

        private async Task SendHeartbeatAsync()
        {
            if (_currentLobby == null || !_isLobbyHost) return;
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Lobby heartbeat: " + exception.Message);
            }
        }

        private void SetStatus(string message)
        {
            LastStatus = message;
            StatusChanged?.Invoke(message);
            Debug.Log("[Rust Survival] " + message);
        }

        private async void OnApplicationQuit()
        {
            if (_currentLobby != null)
                await LeaveLobbyAsync();
        }
    }
}
