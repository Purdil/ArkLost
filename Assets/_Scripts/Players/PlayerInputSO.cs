using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Players
{
    [CreateAssetMenu(fileName = "Player input", menuName = "SO/Player Input", order = 0)]
    public class PlayerInputSO : ScriptableObject, Controls.IPlayerActions
    {
        public event Action<Vector2> OnMovementChange;
        public event Action OnAttackKeyPressed;
        public event Action OnSlideKeyPressed;
        
        public delegate void SkillKeyPress(int keyIndex, bool isPressed);
        public event SkillKeyPress OnSkillKeyPressed;

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
            if (context.started)
            {
                Vector3 pos = GetWorldMousePosition();
                OnMovementChange?.Invoke(new Vector2(pos.x, pos.z));
            }
                
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnAttackKeyPressed?.Invoke();
        }

        public void OnSlide(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnSlideKeyPressed?.Invoke();
        }

        public void OnSkill(InputAction.CallbackContext context)
        {
            int keyIndex = context.action.GetBindingIndexForControl(context.control);
            if (context.performed)
            {
                OnSkillKeyPressed?.Invoke(keyIndex, true);
            }
            else if (context.canceled)
            {
                OnSkillKeyPressed?.Invoke(keyIndex, false);
            }
        }

        public void OnPointer(InputAction.CallbackContext context)
        {
            _screenMousePosition = context.ReadValue<Vector2>();
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
