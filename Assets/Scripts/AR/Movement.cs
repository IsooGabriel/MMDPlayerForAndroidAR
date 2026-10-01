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

    private Vector2 _moveInput;
    private Vector2 _tmpInput;

    private void Awake()
    {
        _inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _inputActions.Enable();

        _inputActions.Player.Move.performed += OnMove;
    }

    private void OnDisable()
    {
        _inputActions.Player.Move.performed -= OnMove;

        _inputActions.Disable();
    }

    private void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            _moveInput = Vector2.zero;
        }

        if (context.performed)
        {
            _tmpInput = context.ReadValue<Vector2>();
            if (_tmpInput.sqrMagnitude < threshold)
            {
                _moveInput = _tmpInput;
            }
        }
    }

    private void Update()
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

        targetTransform.position += new Vector3(moveDirection.x, 0f, moveDirection.y) * moveSpeed * Time.deltaTime;
    }
}
