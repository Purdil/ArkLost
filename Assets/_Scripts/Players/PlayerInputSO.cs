using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Players
{
    [CreateAssetMenu(fileName = "Player input", menuName = "SO/Player Input", order = 0)]
    public class PlayerInputSO : ScriptableObject, Controls.IPlayerActions
    {
        public event Action OnMovementChange;
        public event Action OnAttackKeyPressed;
        public event Action OnDashKeyPressed;
        public event Action OnSkill1Pressed;
        public event Action OnSkill2Pressed;
        public event Action OnSkill3Pressed;
        public event Action OnSkill4Pressed;
        
        public delegate void SkillKeyPress(int keyIndex, bool isPressed);

        [SerializeField] private LayerMask whatIsBoss;
        [SerializeField] private LayerMask whatIsGround;

        private Controls _controls;
        private Vector3 _worldMousePosition;
        private Vector2 _screenMousePosition;

        private Camera _mainCam;

        public Camera MainCam
        {
            get
            {
                if(_mainCam == null)
                    _mainCam = Camera.main;
                return _mainCam;
            }
        }

        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new Controls();
                _controls.Player.SetCallbacks(this);
            }
            _controls.Player.Enable();
        }

        private void OnDisable()
        {
            if(_controls != null)
                _controls.Player.Disable();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnMovementChange?.Invoke();
            }
                
        }

        public void OnDash(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnDashKeyPressed?.Invoke();
        }

        public void OnSkill1(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnSkill1Pressed?.Invoke();
        }

        public void OnSkill2(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnSkill2Pressed?.Invoke();
        }

        public void OnSkill3(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnSkill3Pressed?.Invoke();
        }

        public void OnSkill4(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnSkill4Pressed?.Invoke();
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnAttackKeyPressed?.Invoke();
        }

        public void OnPointer(InputAction.CallbackContext context)
        {
            _screenMousePosition = context.ReadValue<Vector2>();
        }

        public bool CheckPosIsBoss(Vector3 worldPosition, out Collider boss)
        {
            Collider[] hits =
                Physics.OverlapSphere(
                    worldPosition,
                    1.0f,
                    whatIsBoss);

            if (hits.Length > 0)
            {
                boss = hits[0];
                return true;
            }

            boss = null;
            return false;
        }

        public Vector3 GetWorldMousePosition()
        {
            if (MainCam is null)
                return _worldMousePosition;
            
            Ray cameraRay = MainCam.ScreenPointToRay(_screenMousePosition);
            if (Physics.Raycast(cameraRay, out RaycastHit hit, MainCam.farClipPlane, whatIsGround))
            {
                _worldMousePosition = hit.point;
            }
            return _worldMousePosition;
        }
    }
}
