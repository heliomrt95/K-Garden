using UnityEngine;

/// <summary>
/// Contrôleur FPS basique : déplacement WASD + caméra à la souris.
/// Utilise un CharacterController pour gérer les collisions sans physique complexe.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Mouvement")]
    public float walkSpeed = 4.5f;
    public float gravity = -9.81f;

    [Header("Caméra")]
    public Transform cameraTransform;
    public float mouseSensitivity = 2.5f;
    public float maxLookAngle = 85f;

    private CharacterController controller;
    private float verticalVelocity;
    private float pitch; // rotation X de la caméra

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
    }

    private void HandleLook()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mx);

        pitch -= my;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0, 0);
    }

    private void HandleMove()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = transform.right * h + transform.forward * v;
        if (dir.magnitude > 1f) dir.Normalize();

        if (controller.isGrounded && verticalVelocity < 0) verticalVelocity = -1f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = dir * walkSpeed + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }
}
