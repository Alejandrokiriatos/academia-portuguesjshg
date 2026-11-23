using System;
using System.Threading.Tasks;
using RustSurvival.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RustSurvival.Networking
{
    /// <summary>UGUI start menu for offline play and optional Lobby/Relay sessions.</summary>
    public sealed class SessionDebugPanel : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private NetworkSessionManager sessions;
        [SerializeField] private OfflineModeController offlineMode;

        [Header("Menu UI")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text statusLabel;
        [SerializeField] private InputField joinCodeInput;
        [SerializeField] private Button offlineButton;
        [SerializeField] private Button createHostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button leaveButton;

        private bool _busy;

        private void Awake()
        {
            if (sessions == null) sessions = FindObjectOfType<NetworkSessionManager>();
            if (offlineMode == null) offlineMode = FindObjectOfType<OfflineModeController>();

            if (sessions != null)
            {
                sessions.StatusChanged += OnStatusChanged;
                SetStatus(sessions.LastStatus);
            }
            else
            {
                SetStatus("Modo local disponible. Relay requiere NetworkManager.");
            }

            if (offlineButton != null) offlineButton.onClick.AddListener(StartOffline);
            if (createHostButton != null) createHostButton.onClick.AddListener(CreateHost);
            if (joinButton != null) joinButton.onClick.AddListener(JoinLobby);
            if (leaveButton != null) leaveButton.onClick.AddListener(LeaveLobby);

            RefreshButtons();
        }

        private void OnDestroy()
        {
            if (sessions != null)
                sessions.StatusChanged -= OnStatusChanged;

            if (offlineButton != null) offlineButton.onClick.RemoveListener(StartOffline);
            if (createHostButton != null) createHostButton.onClick.RemoveListener(CreateHost);
            if (joinButton != null) joinButton.onClick.RemoveListener(JoinLobby);
            if (leaveButton != null) leaveButton.onClick.RemoveListener(LeaveLobby);
        }

        private void OnStatusChanged(string message)
        {
            SetStatus(message);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;
        }

        private void RefreshButtons()
        {
            bool started = offlineMode != null && offlineMode.HasStarted;
            if (started && panelRoot != null)
                panelRoot.SetActive(false);

            if (offlineButton != null) offlineButton.interactable = !started && !_busy && offlineMode != null;
            if (createHostButton != null) createHostButton.interactable = !started && !_busy && sessions != null;
            if (joinButton != null) joinButton.interactable = !started && !_busy && sessions != null;
            if (leaveButton != null) leaveButton.interactable = !_busy && sessions != null;
            if (joinCodeInput != null) joinCodeInput.interactable = !started && !_busy;
        }

        private void StartOffline()
        {
            if (_busy || offlineMode == null) return;

            offlineMode.StartOfflineGame();
            if (offlineMode.HasStarted)
            {
                SetStatus("Modo local iniciado. ¡Sobrevive!");
                if (panelRoot != null) panelRoot.SetActive(false);
            }
            else
            {
                SetStatus("No se pudo iniciar el mundo. Revisa la consola de Unity.");
            }
            RefreshButtons();
        }

        private void CreateHost()
        {
            if (!_busy)
                _ = CreateHostAsync();
        }

        private async Task CreateHostAsync()
        {
            if (sessions == null) return;
            _busy = true;
            RefreshButtons();

            try
            {
                string code = await sessions.CreateLobbyAndHostAsync("Rust Survival");
                if (joinCodeInput != null) joinCodeInput.text = code;
                SetStatus("Sala creada. Comparte este código: " + code);
            }
            catch (Exception exception)
            {
                SetStatus("No se pudo crear la sala: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                _busy = false;
                RefreshButtons();
            }
        }

        private void JoinLobby()
        {
            if (!_busy)
                _ = JoinAsync();
        }

        private async Task JoinAsync()
        {
            if (sessions == null || joinCodeInput == null ||
                string.IsNullOrWhiteSpace(joinCodeInput.text))
            {
                SetStatus("Escribe primero el código de la sala.");
                return;
            }

            _busy = true;
            RefreshButtons();

            try
            {
                await sessions.JoinLobbyAsync(joinCodeInput.text.Trim().ToUpperInvariant());
                SetStatus("Conectado a la sala.");
            }
            catch (Exception exception)
            {
                SetStatus("No se pudo entrar: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                _busy = false;
                RefreshButtons();
            }
        }

        private void LeaveLobby()
        {
            if (!_busy)
                _ = LeaveAsync();
        }

        private async Task LeaveAsync()
        {
            if (sessions == null) return;
            _busy = true;
            RefreshButtons();

            try
            {
                await sessions.LeaveLobbyAsync();
            }
            catch (Exception exception)
            {
                SetStatus("Error al salir: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                _busy = false;
                RefreshButtons();
            }
        }
    }
}
