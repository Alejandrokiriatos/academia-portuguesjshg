using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RustSurvival.Player
{
    /// <summary>
    /// CharacterController movement. Input is read only by the owning client.
    /// In a network session the client sends input; the server performs movement
    /// and NetworkTransform replicates the authoritative result.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : NetworkBehaviour
    {
        public static PlayerMovement LocalPlayer { get; private set; }
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 7.2f;
        [SerializeField, Min(0.1f)] private float crouchSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.25f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float groundedForce = -2f;

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera ownerCamera;
        [SerializeField] private float mouseSensitivity = 0.10f;
        [SerializeField] private float stickLookSpeed = 150f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Mobile")]
        [SerializeField] private MobileJoystick moveJoystick;
        [SerializeField] private MobileLookArea lookArea;

        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;

        private Vector2 _serverMoveInput;
        private float _serverYaw;
        private bool _serverJump;
        private bool _serverSprint;
        private bool _serverCrouch;
        private float _verticalVelocity;
        private float _localYaw;
        private float _localPitch;
        private bool _mobileJumpQueued;
        private bool _mobileSprint;
        private bool _mobileCrouch;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            BuildInputActions();
        }

        private void BuildInputActions()
        {
            _moveAction = new InputAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddBinding("<Gamepad>/leftStick");

            _lookAction = new InputAction("Look", InputActionType.Value);
            _lookAction.AddBinding("<Mouse>/delta");
            _lookAction.AddBinding("<Gamepad>/rightStick");

            _jumpAction = new InputAction("Jump", InputActionType.Button);
            _jumpAction.AddBinding("<Keyboard>/space");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");

            _sprintAction = new InputAction("Sprint", InputActionType.Button);
            _sprintAction.AddBinding("<Keyboard>/leftShift");
            _sprintAction.AddBinding("<Gamepad>/leftStickPress");

            _crouchAction = new InputAction("Crouch", InputActionType.Button);
            _crouchAction.AddBinding("<Keyboard>/leftCtrl");
            _crouchAction.AddBinding("<Gamepad>/buttonEast");
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _lookAction?.Enable();
            _jumpAction?.Enable();
            _sprintAction?.Enable();
            _crouchAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _lookAction?.Disable();
            _jumpAction?.Disable();
            _sprintAction?.Disable();
            _crouchAction?.Disable();
        }

        private void OnDestroy()
        {
            _moveAction?.Dispose();
            _lookAction?.Dispose();
            _jumpAction?.Dispose();
            _sprintAction?.Dispose();
            _crouchAction?.Dispose();
        }

        public override void OnNetworkSpawn()
        {
            bool isLocalPlayer = IsOwner;
            if (isLocalPlayer) LocalPlayer = this;

            if (moveJoystick == null) moveJoystick = FindObjectOfType<MobileJoystick>();
            if (lookArea == null) lookArea = FindObjectOfType<MobileLookArea>();

            if (ownerCamera != null)
            {
                ownerCamera.enabled = isLocalPlayer;
                AudioListener listener = ownerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = isLocalPlayer;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (LocalPlayer == this) LocalPlayer = null;
        }

        private void Update()
        {
            if (IsSpawned && !IsOwner)
                return;

            Vector2 move = _moveAction.ReadValue<Vector2>();
            if (moveJoystick != null && moveJoystick.IsDragging)
                move = moveJoystick.Value;

            UpdateLook();

            bool jump = _jumpAction.WasPressedThisFrame() || _mobileJumpQueued;
            bool sprint = _sprintAction.IsPressed() || _mobileSprint;
            bool crouch = _crouchAction.IsPressed() || _mobileCrouch;
            _mobileJumpQueued = false;

            if (IsSpawned && !IsServer)
            {
                SubmitInputServerRpc(move, _localYaw, jump, sprint, crouch);
            }
            else
            {
                CacheServerInput(move, _localYaw, jump, sprint, crouch);
            }
        }

        private void FixedUpdate()
        {
            // In offline mode this instance simulates itself. In network mode only
            // the server's copy reaches this point with cached owner input.
            if (IsSpawned && !IsServer)
                return;

            SimulateMovement(_serverMoveInput, _serverYaw, _serverJump, _serverSprint, _serverCrouch);
            _serverJump = false;
        }

        private void UpdateLook()
        {
            Vector2 look = _lookAction.ReadValue<Vector2>();
            bool mouseActive = Mouse.current != null && Mouse.current.delta.IsActuated();

            if (mouseActive)
            {
                _localYaw += look.x * mouseSensitivity;
                _localPitch -= look.y * mouseSensitivity;
            }
            else
            {
                _localYaw += look.x * stickLookSpeed * Time.deltaTime;
                _localPitch -= look.y * stickLookSpeed * Time.deltaTime;
            }

            if (lookArea != null)
            {
                Vector2 touchDelta = lookArea.ConsumeDelta();
                _localYaw += touchDelta.x * mouseSensitivity;
                _localPitch -= touchDelta.y * mouseSensitivity;
            }

            _localPitch = Mathf.Clamp(_localPitch, minPitch, maxPitch);

            if (cameraPivot != null)
                cameraPivot.rotation = Quaternion.Euler(_localPitch, _localYaw, 0f);

            if (!IsSpawned || IsServer)
                transform.rotation = Quaternion.Euler(0f, _localYaw, 0f);
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SubmitInputServerRpc(Vector2 move, float yaw, bool jump, bool sprint, bool crouch)
        {
            CacheServerInput(move, yaw, jump, sprint, crouch);
        }

        private void CacheServerInput(Vector2 move, float yaw, bool jump, bool sprint, bool crouch)
        {
            // Reject malformed client values before they reach server-side movement.
            if (float.IsNaN(yaw) || float.IsInfinity(yaw)) return;
            if (float.IsNaN(move.x) || float.IsInfinity(move.x) ||
                float.IsNaN(move.y) || float.IsInfinity(move.y))
                move = Vector2.zero;

            _serverMoveInput = Vector2.ClampMagnitude(move, 1f);
            _serverYaw = Mathf.Repeat(yaw, 360f);
            _serverJump |= jump;
            _serverSprint = sprint;
            _serverCrouch = crouch;
        }

        private void SimulateMovement(Vector2 input, float yaw, bool jump, bool sprint, bool crouch)
        {
            if (_controller == null || !_controller.enabled)
                return;

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 planar = (forward * input.y + right * input.x);
            if (planar.sqrMagnitude > 1f) planar.Normalize();

            float speed = crouch ? crouchSpeed : sprint ? sprintSpeed : walkSpeed;

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                    _verticalVelocity = groundedForce;

                if (jump)
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * Time.fixedDeltaTime;
            Vector3 velocity = planar * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.fixedDeltaTime);
        }

        /// <summary>Called by MobileActionButton UI.</summary>
        public void SetMobileButton(MobileActionButton.ActionType action, bool pressed)
        {
            switch (action)
            {
                case MobileActionButton.ActionType.Jump:
                    if (pressed) _mobileJumpQueued = true;
                    break;
                case MobileActionButton.ActionType.Sprint:
                    _mobileSprint = pressed;
                    break;
                case MobileActionButton.ActionType.Crouch:
                    _mobileCrouch = pressed;
                    break;
            }
        }

        public float GetYaw() => _localYaw;
    }
}
