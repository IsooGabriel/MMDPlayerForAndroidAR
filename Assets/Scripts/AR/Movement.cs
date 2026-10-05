using System.Net.NetworkInformation;
using Unity.VisualScripting;
using UnityEngine;

public class Movement : MonoBehaviour
{
    private InputSystem_Actions _inputActions;
    [SerializeField]
    private Transform arCamera;
    [SerializeField]
    private Transform targetTransform;
    [SerializeField]
    private float moveSpeed = 20;
    [SerializeField]
    private float threshold = 0.01f;
    [SerializeField]
    private float verticalSpeed = 1f;
    [SerializeField]
    private int turnSpeed = 1;

    private Vector2 _moveInput;
    private Vector2 _tmpInput;
    private int _verticalMoveInput = 0;
    private int _turnMoveInput = 0;


    private void Awake()
    {
        _inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _inputActions.Enable();

        _inputActions.Player.Move.performed += OnMove;
        _inputActions.Player.Move.canceled += OnMove;

        _inputActions.Player.Up.performed += OnUp;
        _inputActions.Player.Up.canceled += OnUp;

        _inputActions.Player.Down.performed += OnDown;
        _inputActions.Player.Down.canceled += OnDown;

        _inputActions.Player.TurnRigth.performed += OnTurnRigth;
        _inputActions.Player.TurnRigth.canceled += OnTurnRigth;

        _inputActions.Player.TurnLeft.performed += OnTurnLeft;
        _inputActions.Player.TurnLeft.canceled += OnTurnLeft;
    }

    private void OnDisable()
    {
        _inputActions.Player.Move.performed -= OnMove;
        _inputActions.Player.Move.canceled -= OnMove;

        _inputActions.Player.Move.performed -= OnUp;
        _inputActions.Player.Up.canceled -= OnUp;

        _inputActions.Player.Move.performed -= OnDown;
        _inputActions.Player.Down.canceled -= OnDown;

        _inputActions.Player.TurnRigth.performed -= OnTurnRigth;
        _inputActions.Player.TurnRigth.canceled -= OnTurnRigth;

        _inputActions.Player.TurnLeft.performed -= OnTurnLeft;
        _inputActions.Player.TurnLeft.canceled -= OnTurnLeft;

        _inputActions.Disable();
    }

    private void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            Debug.Log("‚Æ‚ß‚½");
            _moveInput = Vector2.zero;
            return;
        }

        if (context.performed)
        {
            _tmpInput = context.ReadValue<Vector2>();

            if (_tmpInput.sqrMagnitude >= threshold)
            {
                _moveInput = _tmpInput;
            }
        }
    }


    private void OnUp(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            _verticalMoveInput = 0;
        }

        if (context.performed)
        {
            _verticalMoveInput = 1;
        }
    }
    private void OnDown(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            _verticalMoveInput = 0;
        }

        if (context.performed)
        {
            _verticalMoveInput = -1;
        }
    }

    private void OnTurnRigth(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            _turnMoveInput = 0;
        }

        if (context.performed)
        {
            _turnMoveInput = -1;
        }
    }
    private void OnTurnLeft(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            _turnMoveInput = 0;
        }

        if (context.performed)
        {
            _turnMoveInput = 1;
        }
    }


    private void HorizontalMovementUpdate()
    {
        Vector3 forward = arCamera.forward;
        Vector3 right = arCamera.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection =
            forward * _moveInput.y +
            right * _moveInput.x;

        targetTransform.position +=
            moveDirection * moveSpeed * Time.deltaTime;
    }

    private void VerticalMovementUpdate()
    {
        if (_verticalMoveInput == 0)
        {
            return;
        }

        targetTransform.position += new Vector3(0, _verticalMoveInput*verticalSpeed*Time.deltaTime, 0);
    }
    
    private void TurnMovementUpdate()
    {
        if( _turnMoveInput == 0)
        {
            return;
        }
        targetTransform.Rotate(0, _turnMoveInput*turnSpeed*Time.deltaTime, 0); 
    }

    private void Update()
    {
        HorizontalMovementUpdate();
        VerticalMovementUpdate();
        TurnMovementUpdate();
    }
}
