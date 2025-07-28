using UnityEngine;
using Mirror;

public class FPSPlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float mouseSensitivity = 2f;
    
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask = 1;
    
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private GameObject playerModel;
    
    // Components
    private CharacterController controller;
    
    // Movement
    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;
    
    // Network variables
    [SyncVar] private Vector3 networkPosition;
    [SyncVar] private Quaternion networkRotation;
    [SyncVar] private float networkCameraRotation;

    public override void OnStartLocalPlayer()
    {
        // Configuration pour le joueur local
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            
            // Désactiver l'AudioListener des autres caméras
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            foreach (AudioListener listener in listeners)
            {
                if (listener != playerCamera.GetComponent<AudioListener>())
                    listener.enabled = false;
            }
        }
        
        // Masquer le modèle pour le joueur local (vue FPS)
        if (playerModel != null)
            playerModel.SetActive(false);
        
        // Verrouiller le curseur
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnStartClient()
    {
        // Configuration pour les autres joueurs
        if (!isLocalPlayer)
        {
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
            
            if (playerModel != null)
                playerModel.SetActive(true);
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        // Auto-assign references si elles ne sont pas définies
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        
        if (cameraHolder == null)
            cameraHolder = playerCamera?.transform.parent;
        
        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.localPosition = new Vector3(0, -1f, 0);
            groundCheck = gc.transform;
        }
    }

    void Update()
    {
        if (isLocalPlayer)
        {
            HandleInput();
            HandleMovement();
            HandleMouseLook();
        }
        else
        {
            // Interpolation smooth pour les autres joueurs
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 10f);
            
            if (cameraHolder != null)
            {
                Vector3 currentEuler = cameraHolder.localEulerAngles;
                currentEuler.x = Mathf.LerpAngle(currentEuler.x, networkCameraRotation, Time.deltaTime * 10f);
                cameraHolder.localEulerAngles = currentEuler;
            }
        }
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool isLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isLocked;
        }
    }

    void HandleMovement()
    {
        // Vérification du sol
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;
        
        // Input de mouvement
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        
        // Direction basée sur la rotation du joueur
        Vector3 move = transform.right * x + transform.forward * z;
        
        // Vitesse (marche/course)
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        
        // Appliquer le mouvement
        controller.Move(move * speed * Time.deltaTime);
        
        // Saut
        if (Input.GetButtonDown("Jump") && isGrounded)
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        
        // Gravité
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
        
        // Synchroniser avec le réseau
        if (transform.hasChanged)
        {
            CmdUpdatePosition(transform.position, transform.rotation, xRotation);
            transform.hasChanged = false;
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Rotation horizontale du corps
        transform.Rotate(Vector3.up * mouseX);
        
        // Rotation verticale de la caméra
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    [Command]
    void CmdUpdatePosition(Vector3 pos, Quaternion rot, float camRot)
    {
        networkPosition = pos;
        networkRotation = rot;
        networkCameraRotation = camRot;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}