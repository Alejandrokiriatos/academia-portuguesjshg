using System;
using System.Threading.Tasks;
using UnityEngine;

namespace RustSurvival.Networking
{
    /// <summary>Temporary in-game development panel. Replace with the finished menu UI later.</summary>
    public sealed class SessionDebugPanel : MonoBehaviour
    {
        [SerializeField] private NetworkSessionManager sessions;
        [SerializeField] private bool showPanel = true;

        private string _joinCode = string.Empty;
        private string _status = "Listo";
        private bool _busy;

        private void Awake()
        {
            if (sessions == null) sessions = FindObjectOfType<NetworkSessionManager>();
            if (sessions != null)
            {
                _status = sessions.LastStatus;
                sessions.StatusChanged += OnStatusChanged;
            }
        }

        private void OnDestroy()
        {
            if (sessions != null)
                sessions.StatusChanged -= OnStatusChanged;
        }

        private void OnStatusChanged(string message) => _status = message;

        private void OnGUI()
        {
            if (!showPanel || !Application.isPlaying) return;

            GUILayout.BeginArea(new Rect(16, 16, 340, 230), GUI.skin.box);
            GUILayout.Label("RUST SURVIVAL — MULTIPLAYER DEV");
            GUILayout.Label(_status);

            GUI.enabled = !_busy && sessions != null;
            if (GUILayout.Button("CREAR SALA / HOST (Relay + Lobby)", GUILayout.Height(34)))
                _ = CreateHostAsync();

            GUILayout.Space(6);
            GUILayout.Label("Código de sala:");
            _joinCode = GUILayout.TextField(_joinCode, 12).ToUpperInvariant();
            if (GUILayout.Button("UNIRSE A SALA", GUILayout.Height(32)))
                _ = JoinAsync();

            if (GUILayout.Button("SALIR / DESCONECTAR", GUILayout.Height(28)))
                _ = LeaveAsync();

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private async Task CreateHostAsync()
        {
            if (_busy || sessions == null) return;
            _busy = true;
            try
            {
                string code = await sessions.CreateLobbyAndHostAsync("Rust Survival");
                _joinCode = code;
            }
            catch (Exception exception)
            {
                _status = exception.Message;
                Debug.LogException(exception);
            }
            finally { _busy = false; }
        }

        private async Task JoinAsync()
        {
            if (_busy || sessions == null || string.IsNullOrWhiteSpace(_joinCode)) return;
            _busy = true;
            try { await sessions.JoinLobbyAsync(_joinCode); }
            catch (Exception exception)
            {
                _status = exception.Message;
                Debug.LogException(exception);
            }
            finally { _busy = false; }
        }

        private async Task LeaveAsync()
        {
            if (_busy || sessions == null) return;
            _busy = true;
            try { await sessions.LeaveLobbyAsync(); }
            catch (Exception exception)
            {
                _status = exception.Message;
                Debug.LogException(exception);
            }
            finally { _busy = false; }
        }
    }
}
