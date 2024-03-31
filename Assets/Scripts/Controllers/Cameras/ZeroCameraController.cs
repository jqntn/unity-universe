#nullable enable

using UnityEngine;
using Unity.Mathematics;
using System.Linq;
using Zero.Controllers.Cameras;
using Zero.Services.Base;
using Zero.Services;
using Zero.Utils;
using System.Collections.Generic;
using Zero.GIS;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#if UNITY_IOS || UNITY_ANDROID
#endif
#endif

namespace CesiumForUnity
{
    /// <summary>
    /// A camera controller that can easily move around and view the globe while
    /// maintaining a sensible orientation. As the camera moves across the horizon,
    /// it automatically changes its own up direction such that the world always
    /// looks right-side up.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    internal sealed class ZeroCameraController : MonoBehaviour, ICameraController
    {
        private static readonly LazyService<ICameraService> CAMERA_SERVICE = new();

        public Camera Camera { get; private set; } = null!;

        public HashSet<CelestialBody> BodiesInRange { get; } = new();

        #region User-editable properties

        /// <summary>
        /// Whether movement is enabled on this controller. Movement is
        /// controlled using the W, A, S, D keys, as well as the Q and E
        /// keys for vertical movement with respect to the globe.
        /// </summary>
        public bool EnableMovement
        { get => _enableMovement; set { _enableMovement = value; ResetSpeed(); } }
        [SerializeField] private bool _enableMovement = true;

        /// <summary>
        /// Whether rotation is enabled on this controller. Rotation is
        /// controlled by movement of the mouse.
        /// </summary>
        public bool EnableRotation
        { get => _enableRotation; set => _enableRotation = value; }
        [SerializeField] private bool _enableRotation = true;

        /// <summary>
        /// The maximum speed of this controller when dynamic speed is disabled.
        /// If dynamic speed is enabled, this value will not be used.
        /// </summary>
        public float DefaultMaximumSpeed
        { get => _defaultMaximumSpeed; set => _defaultMaximumSpeed = math.max(value, 0.0f); }
        [SerializeField][Min(0.0f)] private float _defaultMaximumSpeed = 100.0f;

        /// <summary>
        /// Whether to enable dynamic speed on this controller. If enabled,
        /// the controller's speed will change dynamically based on elevation
        /// and other factors.
        /// </summary>
        public bool EnableDynamicSpeed
        { get => _enableDynamicSpeed; set => _enableDynamicSpeed = value; }
        [SerializeField] private bool _enableDynamicSpeed = true;

        /// <summary>
        /// The minimum height where dynamic speed starts to take effect.
        /// Below this height, the speed will be set to the object's height
        /// from the Earth, which makes it move slowly when it is right above a tileset.
        /// </summary>
        public float DynamicSpeedMinHeight
        { get => _dynamicSpeedMinHeight; set => _dynamicSpeedMinHeight = math.max(value, 0.0f); }
        [SerializeField][Min(0.0f)] private float _dynamicSpeedMinHeight = 20.0f;

        /// <summary>
        /// Whether to dynamically adjust the camera's clipping planes so that
        /// the globe will not be clipped from far away. Objects that are close
        /// to the camera but far above the globe in space may not appear.
        /// </summary>
        public bool EnableDynamicClippingPlanes
        { get => _enableDynamicClippingPlanes; set => _enableDynamicClippingPlanes = value; }
        [SerializeField] private bool _enableDynamicClippingPlanes = true;

        /// <summary>
        /// The height to start dynamically adjusting the camera's clipping planes.
        /// Below this height, the clipping planes will be set to their initial values.
        /// </summary>
        public float DynamicClippingPlanesMinHeight
        { get => _dynamicClippingPlanesMinHeight; set => _dynamicClippingPlanesMinHeight = math.max(value, 0.0f); }
        [SerializeField][Min(0.0f)] private float _dynamicClippingPlanesMinHeight = 10_000.0f;

        public float DynamicClippingPlanesRadius
        { get => _dynamicClippingPlanesRadius; set => _dynamicClippingPlanesRadius = math.max(value, 0.0f); }
        [SerializeField][Min(0.0f)] private float _dynamicClippingPlanesRadius = (float)UnitsUtils.ONE_AU;

#if ENABLE_INPUT_SYSTEM
        [SerializeField][HideInInspector] private InputActionProperty _lookAction;
        [SerializeField][HideInInspector] private InputActionProperty _moveAction;
        [SerializeField][HideInInspector] private InputActionProperty _moveUpAction;
        [SerializeField][HideInInspector] private InputActionProperty _speedChangeAction;
        [SerializeField][HideInInspector] private InputActionProperty _speedResetAction;
        [SerializeField][HideInInspector] private InputActionProperty _toggleDynamicSpeedAction;
#endif

        #endregion User-editable properties

        #region Private variables

        private const float LOOK_SPEED_MULTIPLIER = 2.0f;
        private const float MOVE_SPEED_MULTIPLIER = 8.0f;

        private float _initialNearClipPlane;
        private float _initialFarClipPlane;

        private CharacterController _controller = null!;
        private CesiumGeoreference _georeference = null!;
        private CesiumGlobeAnchor _globeAnchor = null!;

        private Vector3 _velocity = Vector3.zero;
        private readonly float _lookSpeed = 10.0f * LOOK_SPEED_MULTIPLIER;

        private float _acceleration = 10_000.0f;
        private readonly float _deceleration = math.INFINITY;
        private readonly float _maxRaycastDistance = 1_000_000.0f;

        private float _maxSpeed = 100.0f; // Maximum speed with the speed multiplier applied.
        private float _maxSpeedPreMultiplier = 0.0f; // Max speed without the multiplier applied.
        private AnimationCurve _maxSpeedCurve = null!;

        private float _speedMultiplier = 1.0f;
        private readonly float _speedMultiplierIncrement = 1.5f;

        // If the near clip gets too large, Unity will throw errors. Keeping it
        // at this value works fine even when the far clip plane gets large.
        private readonly float _maximumNearClipPlane = 1e+10f;
        private readonly float _maximumFarClipPlane = 1e+10f;

        // The maximum ratio that the far clip plane is allowed to be larger
        // than the near clip plane. The near clip plane is set so that this
        // ratio is never exceeded.
        private readonly float _maximumNearToFarRatio = 100_000.0f;

        #endregion Private variables

        #region Input configuration

#if ENABLE_INPUT_SYSTEM
        private bool HasInputAction(in InputActionProperty property)
        {
            return (property.action != null && property.action.bindings.Any()) || (property.reference != null);
        }

        private void ConfigureInputs()
        {
#if UNITY_IOS || UNITY_ANDROID
            EnhancedTouch.EnhancedTouchSupport.Enable();
#endif
            InputActionMap map = new("Cesium Camera Controller");

            if (!HasInputAction(_lookAction))
            {
                InputAction newLookAction = map.AddAction("look", binding: "<Mouse>/delta");
                newLookAction.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=15, y=15)");
                _lookAction = new InputActionProperty(newLookAction);
            }

            if (!HasInputAction(_moveAction))
            {
                InputAction newMoveAction = map.AddAction("move", binding: "<Gamepad>/leftStick");
                newMoveAction.AddCompositeBinding("Dpad")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
                _moveAction = new InputActionProperty(newMoveAction);
            }

            if (!HasInputAction(_moveUpAction))
            {
                InputAction newMoveUpAction = map.AddAction("moveUp");
                newMoveUpAction.AddCompositeBinding("Dpad")
                    .With("Up", "<Keyboard>/space")
                    .With("Down", "<Keyboard>/c")
                    .With("Up", "<Keyboard>/e")
                    .With("Down", "<Keyboard>/q")
                    .With("Up", "<Gamepad>/rightTrigger")
                    .With("Down", "<Gamepad>/leftTrigger");
                _moveUpAction = new InputActionProperty(newMoveUpAction);
            }

            if (!HasInputAction(_speedChangeAction))
            {
                InputAction newSpeedChangeAction = map.AddAction("speedChange", binding: "<Mouse>/scroll");
                newSpeedChangeAction.AddCompositeBinding("Dpad")
                    .With("Up", "<Gamepad>/rightShoulder")
                    .With("Down", "<Gamepad>/leftShoulder");
                _speedChangeAction = new InputActionProperty(newSpeedChangeAction);
            }

            if (!HasInputAction(_speedResetAction))
            {
                InputAction newSpeedResetAction = map.AddAction("speedReset", binding: "<Mouse>/middleButton");
                newSpeedResetAction.AddBinding("<Gamepad>/buttonNorth");
                _speedResetAction = new InputActionProperty(newSpeedResetAction);
            }

            if (!HasInputAction(_toggleDynamicSpeedAction))
            {
                InputAction newToggleDynamicSpeedAction =
                    map.AddAction("toggleDynamicSpeed", binding: "<Keyboard>/g");
                newToggleDynamicSpeedAction.AddBinding("<Gamepad>/buttonEast");
                _toggleDynamicSpeedAction = new InputActionProperty(newToggleDynamicSpeedAction);
            }

            _moveAction.action.Enable();
            _lookAction.action.Enable();
            _moveUpAction.action.Enable();
            _speedChangeAction.action.Enable();
            _speedResetAction.action.Enable();
            _toggleDynamicSpeedAction.action.Enable();
        }
#endif

        #endregion Input configuration

        #region Initialization

        private void InitializeCamera()
        {
            Camera = gameObject.GetComponent<Camera>();
            _initialNearClipPlane = Camera.nearClipPlane;
            _initialFarClipPlane = Camera.farClipPlane;
        }

        private void InitializeController()
        {
            if (_globeAnchor.GetComponent<CharacterController>() != null)
            {
                Debug.LogWarning(
                    "A CharacterController component was manually " +
                    "added to the CesiumGlobeAnchor's game object. " +
                    "This may interfere with the CesiumCameraController's movement.");

                _controller = _globeAnchor.GetComponent<CharacterController>();
            }
            else
            {
                _controller = _globeAnchor.gameObject.AddComponent<CharacterController>();
                _controller.hideFlags = HideFlags.HideInInspector;
            }

            _controller.radius = 1.0f;
            _controller.height = 1.0f;
            _controller.center = Vector3.zero;
            _controller.detectCollisions = true;
        }

        /// <summary>
        /// Creates a curve to control the bounds of the maximum speed before it is
        /// multiplied by the speed multiplier. This prevents the camera from achieving
        /// an unreasonably low or high speed.
        /// </summary>
        private void CreateMaxSpeedCurve()
        {
            // This creates a curve that is linear between the first two keys,
            // then smoothly interpolated between the last two keys.
            Keyframe[] keyframes = {
                new(0.0f, 4.0f),
                new(10000000.0f, 10000000.0f),
                new(13000000.0f, 2000000.0f)
            };

            keyframes[0].weightedMode = WeightedMode.Out;
            keyframes[0].outTangent = keyframes[1].value / keyframes[0].value;
            keyframes[0].outWeight = 0.0f;

            keyframes[1].weightedMode = WeightedMode.In;
            keyframes[1].inWeight = 0.0f;
            keyframes[1].inTangent = keyframes[1].value / keyframes[0].value;
            keyframes[1].outTangent = 0.0f;

            keyframes[2].inTangent = 0.0f;

            _maxSpeedCurve = new AnimationCurve(keyframes)
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.ClampForever
            };
        }

        private async void Awake()
        {
            await ServiceUtils.WaitForDependency<ICameraService>();

            _georeference = gameObject.GetComponentInParent<CesiumGeoreference>();
            if (_georeference == null)
            {
                Debug.LogError(
                    "CesiumCameraController must be nested under a game object " +
                    "with a CesiumGeoreference.");
            }

            _globeAnchor = gameObject.GetComponentInParent<CesiumGlobeAnchor>();
            if (_globeAnchor == null)
            {
                Debug.LogError("CesiumCameraController must have a CesiumGlobeAnchor " +
                    "attached to itself or a parent");
            }
            else if (!_globeAnchor.TryGetComponent<ZeroOriginShift>(out _))
            {
                Debug.LogError("CesiumCameraController expects a CesiumOriginShift " +
                    $"on {(_globeAnchor != null ? _globeAnchor.name : null)}, none found");
            }

            InitializeCamera();
            InitializeController();
            CreateMaxSpeedCurve();

#if ENABLE_INPUT_SYSTEM
            ConfigureInputs();
#endif

            CAMERA_SERVICE.Value.RegisterCameraController(this);
        }

#if UNITY_EDITOR
        // Ensures required components are present in the editor.
        private void Reset()
        {
            CesiumGlobeAnchor anchor = gameObject.GetComponentInParent<CesiumGlobeAnchor>();
            if (anchor == null)
            {
                anchor = gameObject.AddComponent<CesiumGlobeAnchor>();
                Debug.LogWarning("CesiumCameraController missing a CesiumGlobeAnchor - adding");
            }

            if (!anchor.TryGetComponent<CesiumOriginShift>(out _))
            {
                _ = anchor.gameObject.AddComponent<CesiumOriginShift>();
                Debug.LogWarning("CesiumCameraController missing a CesiumOriginShift - adding");
            }
        }
#endif

        #endregion Initialization

        #region Update

        private void Update()
        {
            HandlePlayerInputs();

            if (_enableDynamicClippingPlanes)
            {
                UpdateClippingPlanes();
            }
        }

        #endregion Update

        #region Raycasting helpers

        private bool RaycastTowardsEarthCenter(out float hitDistance)
        {
            CelestialBody closestBody = BodiesInRange
                .OrderBy(x => (transform.position - x.transform.position).sqrMagnitude)
                .FirstOrDefault();

            double3 position = closestBody != null ? closestBody.GlobeAnchor.positionGlobeFixed : double3.zero;
            double3 center = _georeference.TransformEarthCenteredEarthFixedPositionToUnity(position);

            if (Physics.Linecast(transform.position, (float3)center, out RaycastHit hitInfo))
            {
                hitDistance = math.distance(transform.position, hitInfo.point);
                return true;
            }

            hitDistance = 0.0f;
            return false;
        }

        private bool RaycastAlongForwardVector(float raycastDistance, out float hitDistance)
        {
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hitInfo, raycastDistance))
            {
                hitDistance = math.distance(transform.position, hitInfo.point);
                return true;
            }

            hitDistance = 0.0f;
            return false;
        }

        #endregion Raycasting helpers

        #region Player movement

        private void HandlePlayerInputs()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 lookDelta;
            Vector2 moveDelta;

            lookDelta = _lookAction.action.ReadValue<Vector2>();
            moveDelta = _moveAction.action.ReadValue<Vector2>();

#if UNITY_IOS || UNITY_ANDROID
            bool handledMove = false;
            bool handledLook = false;

            foreach (EnhancedTouch.Touch touch in EnhancedTouch.Touch.activeTouches)
            {
                if (touch.startScreenPosition.x < Screen.width / 2)
                {
                    if (!handledMove)
                    {
                        handledMove = true;
                        moveDelta = touch.screenPosition - touch.startScreenPosition;
                    }
                }
                else
                {
                    if (!handledLook)
                    {
                        handledLook = true;
                        lookDelta = touch.delta;
                    }
                }
            }
#endif

            float inputRotateHorizontal = lookDelta.x;
            float inputRotateVertical = lookDelta.y;

            float inputForward = moveDelta.y;
            float inputRight = moveDelta.x;

            float inputUp = _moveUpAction.action.ReadValue<Vector2>().y;

            float inputSpeedChange = _speedChangeAction.action.ReadValue<Vector2>().y;
            bool inputSpeedReset = _speedResetAction.action.ReadValue<float>() > 0.5f;

            bool toggleDynamicSpeed = _toggleDynamicSpeedAction.action.ReadValue<float>() > 0.5f;
#else
            float inputRotateHorizontal = Input.GetAxis("Mouse X");
            float inputRotateVertical = Input.GetAxis("Mouse Y");

            float inputForward = Input.GetAxis("Vertical");
            float inputRight = Input.GetAxis("Horizontal");
            float inputUp = 0.0f;

            if (Input.GetKeyDown(KeyCode.Q))
            {
                inputUp -= 1.0f;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                inputUp += 1.0f;
            }

            float inputSpeedChange = Input.GetAxis("Mouse ScrollWheel");
            bool inputSpeedReset =
                Input.GetMouseButtonDown(2) || Input.GetKeyDown(KeyCode.JoystickButton3);

            bool toggleDynamicSpeed =
                Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.JoystickButton1);
#endif

            Vector3 movementInput = new(inputRight, inputUp, inputForward);

            if (_enableRotation)
            {
                Rotate(inputRotateHorizontal, inputRotateVertical);
            }

            if (toggleDynamicSpeed)
            {
                _enableDynamicSpeed = !_enableDynamicSpeed;
            }

            if (inputSpeedReset || (_enableDynamicSpeed && movementInput == Vector3.zero))
            {
                ResetSpeedMultiplier();
            }
            else
            {
                HandleSpeedChange(inputSpeedChange);
            }

            if (_enableMovement)
            {
                Move(movementInput);
            }
        }

        private void HandleSpeedChange(float speedChangeInput)
        {
            if (_enableDynamicSpeed)
            {
                UpdateDynamicSpeed();
            }
            else
            {
                SetMaxSpeed(_defaultMaximumSpeed);
            }

            if (speedChangeInput != 0.0f)
            {
                if (speedChangeInput > 0.0f)
                {
                    _speedMultiplier *= _speedMultiplierIncrement;
                }
                else
                {
                    _speedMultiplier /= _speedMultiplierIncrement;
                }

                float max = _enableDynamicSpeed ? 50.0f : 50000.0f;
                _speedMultiplier = math.clamp(_speedMultiplier, 0.1f, max);
            }
        }

        /// <summary>
        /// Rotate the camera with the specified amounts.
        /// </summary>
        /// <remarks>
        /// Horizontal rotation (i.e. looking left or right) corresponds to rotation around the Y-axis.
        /// Vertical rotation (i.e. looking up or down) corresponds to rotation around the X-axis.
        /// </remarks>
        /// <param name="horizontalRotation">The amount to rotate horizontally, i.e. around the Y-axis.</param>
        /// <param name="verticalRotation">The amount to rotate vertically, i.e. around the X-axis.</param>
        private void Rotate(float horizontalRotation, float verticalRotation)
        {
            if (horizontalRotation == 0.0f && verticalRotation == 0.0f)
            {
                return;
            }

            float valueX = verticalRotation * _lookSpeed * Time.deltaTime;
            float valueY = horizontalRotation * _lookSpeed * Time.deltaTime;

            // Rotation around the X-axis occurs counter-clockwise, so the look range
            // maps to [270, 360] degrees for the upper quarter-sphere of motion, and
            // [0, 90] degrees for the lower. Euler angles only work with positive values,
            // so map the [0, 90] range to [360, 450] so the entire range is [270, 450].
            // This makes it easy to clamp the values.
            float rotationX = transform.localEulerAngles.x;
            if (rotationX <= 90.0f)
            {
                rotationX += 360.0f;
            }

            float newRotationX = math.clamp(rotationX - valueX, 270.0f, 450.0f);
            float newRotationY = transform.localEulerAngles.y + valueY;
            transform.localRotation = Quaternion.Euler(newRotationX, newRotationY, transform.localEulerAngles.z);
        }

        /// <summary>
        /// Moves the controller with the given player input.
        /// </summary>
        /// <remarks>
        /// The x-coordinate affects movement along the transform's right axis.
        /// The y-coordinate affects movement along the georeferenced up axis.
        /// The z-coordinate affects movement along the transform's forward axis.
        /// </remarks>
        /// <param name="movementInput">The player input.</param>
        private void Move(in Vector3 movementInput)
        {
            Vector3 inputDirection = transform.right * movementInput.x + transform.forward * movementInput.z;

            if (_georeference != null)
            {
                double3 positionECEF = _globeAnchor.positionGlobeFixed;
                double3 upECEF = CesiumWgs84Ellipsoid.GeodeticSurfaceNormal(positionECEF);
                double3 upUnity = _georeference.TransformEarthCenteredEarthFixedDirectionToUnity(upECEF);

                inputDirection = (float3)inputDirection + (float3)upUnity * movementInput.y;
            }

            if (inputDirection != Vector3.zero)
            {
                // If the controller was already moving, handle the direction change
                // separately from the magnitude of the velocity.
                if (_velocity.magnitude > 0.0f)
                {
                    Vector3 directionChange = inputDirection - _velocity.normalized;
                    _velocity += _velocity.magnitude * Time.deltaTime * MOVE_SPEED_MULTIPLIER * directionChange;
                }

                _velocity += _acceleration * Time.deltaTime * MOVE_SPEED_MULTIPLIER * inputDirection;
                _velocity = Vector3.ClampMagnitude(_velocity, _maxSpeed);
            }
            else
            {
                // Decelerate
                float speed = math.max(_velocity.magnitude - _deceleration * Time.deltaTime * MOVE_SPEED_MULTIPLIER, 0.0f);

                _velocity = Vector3.ClampMagnitude(_velocity, speed);
            }

            if (_velocity != Vector3.zero)
            {
                _controller.Move(Time.deltaTime * MOVE_SPEED_MULTIPLIER * _velocity);

                // Other controllers may disable detectTransformChanges to control their own
                // movement, but the globe anchor should be synced even if detectTransformChanges
                // is false.
                if (!_globeAnchor.detectTransformChanges)
                {
                    _globeAnchor.Sync();
                }
            }
        }

        #endregion Player movement

        #region Dynamic speed computation

        /// <summary>
        /// Gets the dynamic speed of the controller based on the camera's height from
        /// the earth's center and its distance from objects along the forward vector.
        /// </summary>
        /// <param name="overrideSpeed">Whether the returned speed should override the
        /// previous speed, even if the new value is lower.</param>
        /// <param name="newSpeed">The new dynamic speed of the controller.</param>
        /// <returns>Whether a valid speed value was found.</returns>
        private bool GetDynamicSpeed(out bool overrideSpeed, out float newSpeed)
        {
            if (_georeference == null)
            {
                overrideSpeed = false;
                newSpeed = 0.0f;

                return false;
            }

            // Raycast from the camera to the Earth's center and compute the distance.
            // Ignore the result if the height is approximately 0.
            if (!RaycastTowardsEarthCenter(out float height) || height < 0.000001f)
            {
                overrideSpeed = false;
                newSpeed = 0.0f;

                return false;
            }

            // Also ignore the result if the speed will increase or decrease by too much at once.
            // This can be an issue when 3D tiles are loaded/unloaded from the scene.
            if (_maxSpeedPreMultiplier > 0.5f)
            {
                float heightToMaxSpeedRatio = height / _maxSpeedPreMultiplier;

                // The asymmetry of these ratios is intentional. When traversing tilesets
                // with many height differences (e.g. a city with tall buildings), flying over
                // taller geometry will cause the camera to slow down suddenly, and sometimes
                // cause it to stutter.
                if (heightToMaxSpeedRatio > 1000.0f || heightToMaxSpeedRatio < 0.01f)
                {
                    overrideSpeed = false;
                    newSpeed = 0.0f;

                    return false;
                }
            }

            // Raycast along the camera's view (forward) vector.
            float raycastDistance = math.clamp(_maxSpeed * 3.0f, 0.0f, _maxRaycastDistance);

            // If the raycast does not hit, then only override speed if the height
            // is lower than the maximum threshold. Otherwise, if both raycasts hit,
            // always override the speed.
            if (!RaycastAlongForwardVector(raycastDistance, out float viewDistance) || viewDistance < 0.000001f)
            {
                overrideSpeed = height <= _dynamicSpeedMinHeight;
            }
            else
            {
                overrideSpeed = true;
            }

            // Set the speed to be the height of the camera from the Earth's center.
            newSpeed = height;

            return true;
        }

        private void ResetSpeedMultiplier()
        {
            _speedMultiplier = 1.0f;
        }

        private void SetMaxSpeed(float speed)
        {
            float actualSpeed = _maxSpeedCurve.Evaluate(speed);
            _maxSpeed = _speedMultiplier * actualSpeed;
            _acceleration = math.clamp(_maxSpeed * 5.0f, 20000.0f, 10000000.0f);
        }

        private void UpdateDynamicSpeed()
        {
            if (GetDynamicSpeed(out bool overrideSpeed, out float newSpeed))
            {
                if (overrideSpeed || newSpeed >= _maxSpeedPreMultiplier)
                {
                    _maxSpeedPreMultiplier = newSpeed;
                }
            }

            SetMaxSpeed(_maxSpeedPreMultiplier);
        }

        private void ResetSpeed()
        {
            _maxSpeed = _defaultMaximumSpeed;
            _maxSpeedPreMultiplier = 0.0f;
            ResetSpeedMultiplier();
        }

        #endregion Dynamic speed computation

        #region Dynamic clipping plane adjustment

        private void UpdateClippingPlanes()
        {
            if (Camera == null)
            {
                return;
            }

            // Raycast from the camera to the Earth's center and compute the distance.
            if (!RaycastTowardsEarthCenter(out float height))
            {
                return;
            }

            float nearClipPlane = _initialNearClipPlane;
            float farClipPlane = _initialFarClipPlane;

            if (height >= _dynamicClippingPlanesMinHeight)
            {
                double radius = _dynamicClippingPlanesRadius;

                if (BodiesInRange.Any())
                {
                    IEnumerable<CelestialBody> surfaceBodiesInRange =
                        BodiesInRange.Where(x => x.Surface != null && x.ReferenceUnit == EReferenceUnit.M);
                    if (surfaceBodiesInRange.Any())
                    {
                        radius = surfaceBodiesInRange.Max(x => x.MaxRadius);
                    }
                }

                farClipPlane = height + (float)(2.0 * radius);
                farClipPlane = math.min(farClipPlane, _maximumFarClipPlane);

                float farClipRatio = farClipPlane / _maximumNearToFarRatio;

                if (farClipRatio > nearClipPlane)
                {
                    nearClipPlane = math.min(farClipRatio, _maximumNearClipPlane);
                }
            }

            Camera.nearClipPlane = nearClipPlane;
            Camera.farClipPlane = farClipPlane;
        }

        #endregion Dynamic clipping plane adjustment
    }
}