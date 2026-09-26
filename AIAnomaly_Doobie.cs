using System.Collections.Generic;
using UnityEngine;

public enum DoobieStates
{
    Idle,       // dormant or stationary. Either waiting for a trigger or hard-set to be inactive. Virtually never happens on its own (? remains to be seen)
    Patrol,     // wandering the  navigation nodes graph randomly
    Distracted, // detected something suspicious. deciding whether to pursue or ignore
    Hunt,       // actively pursuing the player
    Sequence,   // playing an animated/scripted sequence
}

public partial class AIAnomaly_Doobie : MonoBehaviour
{
    //MASTER FILE

    public DoobieStates currentState = DoobieStates.Idle;
    private DoobieStates previousState = DoobieStates.Patrol;

    //==============================
    // REFERENCES
    //==============================
    [SerializeField] private Transform root;
    [SerializeField] private GameObject myEyes;         // where vision checks originate
    [SerializeField] private GameObject myTarget;       // usually the player
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioBody;     // creature vocals; doesn't make move sounds
    private GameObject mainCamera;                      // cached at runtime

    //==============================
    // MOVEMENT
    //==============================
    private bool wantsToMove = false;
    private bool isMoving = false;
    [SerializeField] private float turnSpeed = 15f;
    [SerializeField] private float moveSpeed = 1f;

    // NEW: Geometry detection (doobie phases thru walls)
    [SerializeField] private LayerMask solidGeometryMask;  // Walls, floors, ceilings, large props. NOT the player
    [SerializeField] private float insideGeometrySpeedMultiplier = 0.2f;
    private bool isInsideSolidGeometry = false;
    private BoxCollider myBox;                              // cached reference
    private Collider[] overlapBuffer = new Collider[8];     // pre-allocated so that there's zero garbage per frame

    //==============================
    // ANIMATIONS
    //==============================
    private int currentAnimHash = 0;

    //==============================
    // SIGHT
    //==============================
    [SerializeField] private bool sightLOS = false;
    [SerializeField] private float sightFOV = 120f;         // anomalies perceive more than humans
    [SerializeField] private float sightRange = 10f;
    [SerializeField] private float sightLossTime = 10f;     // time until the player is considered lost
    private float currentLossTimer = 0f;
    [SerializeField] private LayerMask sightLayerMask;

    //==============================
    // DISTRACTED
    //==============================
    [SerializeField] private float disDeliberationTimeMin = 0.3f;
    [SerializeField] private float disDeliberationTimeMax = 1.5f;
    private float disDeliberationTimer = 0f;
    [SerializeField] private float disToIdleChance = 0.4f;  // chance to return to Idle; else Hunt

    //==============================
    // NAVIGATION - Node-based
    //==============================
    [SerializeField] private GameObject navCurrentNode;
    [SerializeField] private GameObject navTargetNode;
    [SerializeField] private bool navReachedTargetNode;
    [SerializeField] public List<NavigationNode> currentPath = new List<NavigationNode>();
    private bool isLookingAtNavTarget;
    private NavigationNode lastNavTarget;

    //==============================
    // HUNT
    //==============================
    [SerializeField] private float huntCatchDistance = 1.0f;
    [SerializeField] private Vector3 lastKnownPlayerPos;

    // NEW: Teleport parameters
    [SerializeField] private float teleportTriggerRange = 3f;   // 3m around sightRange
    [SerializeField] private float teleportSearchRadius = 8f;   // how close to the player the teleport node must be
    [SerializeField] private float teleportCooldown = 10f;      // seconds between allowed teleports
    private float teleportCooldownTimer = 0f;

    private void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        myBox = GetComponent<BoxCollider>();
        player = myTarget.GetComponent<PlayerController>();

        mainCamera = Camera.main.gameObject;
}

    void Update()
    {
        switch (currentState)
        {
            case DoobieStates.Idle: UpdateIdle(); break;
            case DoobieStates.Patrol: UpdatePatrol(); break;
            case DoobieStates.Distracted: UpdateDistracted(); break;
            case DoobieStates.Hunt: UpdateHunt(); break;
        }

        UpdateAudio();
    }

    public void ChangeState(DoobieStates newState)
    {
        if (currentState == newState) return;
        previousState = currentState; // remember where we came from
        currentState = newState;
    }


    #region Universal Methods

    /// <summary>
    /// Checks whether Doobie's position is currently within the player's camera viewport.
    /// If false, the player has looked away and Doobie can teleport
    /// </summary>
    private bool IsPlayerLookingAtMe()
    {
        if (myTarget == null) return false;

        Camera playerCam = myTarget.GetComponentInChildren<Camera>();
        if (playerCam == null) return false;

        // Project Doobie's position into the camera's viewport space.
        // Viewport coordinates: (0,0) is bottom-left, (1,1) is top-right.
        // Z > 0 means the point is in front of the camera.
        Vector3 viewportPoint = playerCam.WorldToViewportPoint(root.position);

        bool onScreen = viewportPoint.x > 0f && viewportPoint.x < 1f
                     && viewportPoint.y > 0f && viewportPoint.y < 1f
                     && viewportPoint.z > 0f;

        return onScreen;
    }

    /// <summary>
    /// Checks whether Doobie's BoxCollider currently overlaps any solid geometry.
    /// </summary>
    private void CheckInsideSolidGeometry()
    {
        if (myBox == null)
        {
            isInsideSolidGeometry = false;
            return;
        }

        // Transform the BoxCollider's local center and half-extents into world space
        Vector3 worldCenter = transform.TransformPoint(myBox.center);
        Vector3 halfExtents = myBox.size * 0.5f;

        int hitCount = Physics.OverlapBoxNonAlloc(
            worldCenter,
            halfExtents,
            overlapBuffer,
            transform.rotation,       // Rotates with Doobie, as you specified
            solidGeometryMask         // Only check layers marked as solid geometry
        );

        // If ANY solid collider overlaps our box, we are inside geometry
        isInsideSolidGeometry = hitCount > 0;
    }

    public bool CheckLineOfSight()
    {
        if (myTarget == null || myEyes == null) return false;

        // 1.If Main Camera doesn't exist or is somehow not assigned, calculate the target's "chest" position (origin + 0.4m up)
        Vector3 targetLook;
        if (mainCamera == null)
        {
            targetLook = myTarget.transform.position + Vector3.up * 0.4f;
        }
        else
        {
            targetLook = mainCamera.transform.position;
        }
        Vector3 dirToTarget = targetLook - myEyes.transform.position;
        float distanceToTarget = dirToTarget.magnitude;

        // 2. Immediate return: Is it too far away?
        if (distanceToTarget > sightRange)
        {
            Debug.DrawLine(myEyes.transform.position, myEyes.transform.position + dirToTarget, Color.red);
            sightLOS = false;
            return false;
        }

        // 3. Immediate return: Is it outside the FOV cone?
        // use myEyes' forward vector.
        float angleToTarget = Vector3.Angle(myEyes.transform.forward, dirToTarget);
        if (angleToTarget > sightFOV / 2f)
        {
            Debug.DrawLine(myEyes.transform.position, myEyes.transform.position + dirToTarget, Color.yellow);
            sightLOS = false;
            return false;
        }

        // 4. Raycast check: Is there geometry in the way?
        Ray sightRay = new Ray(myEyes.transform.position, dirToTarget.normalized);
        // OLD:
        // if (Physics.Raycast(sightRay, out RaycastHit hit, distanceToTarget, sightLayerMask))

        // NEW: Cast a sphere
        if (Physics.SphereCast(sightRay, 0.2f, out RaycastHit hit, distanceToTarget, sightLayerMask))
        {
            Debug.DrawLine(myEyes.transform.position, hit.point, Color.green);

            // If we hit the target (or a child of it, like player collider), LOS is clear
            if (hit.transform == myTarget.transform || hit.transform.IsChildOf(myTarget.transform))
            {
                sightLOS = true;
                return true;
            }

            // Hit a wall or obstacle
            Debug.DrawRay(hit.point, Vector3.up * 0.2f, Color.magenta, 0.1f);
        }
        else
        {
            // No hit
            Debug.DrawLine(myEyes.transform.position, myEyes.transform.position + dirToTarget, Color.cyan);
        }

        sightLOS = false;
        return false;
    }
    public void Move(Transform target)
    {
        if (target == null) return;

        // Refresh geometry status before moving
        CheckInsideSolidGeometry();

        // Visual only: rotate to face the target
        TurnToLookAt(target);

        // Movement: drift directly toward the target
        Vector3 dirToTarget = target.position - root.position;
        dirToTarget.y = 0f;
        float distToTarget = dirToTarget.magnitude;
        if (distToTarget < 0.05f) return;

        dirToTarget.Normalize();

        // Apply speed penalty when phasing through solid geometry
        float currentSpeed = moveSpeed;
        if (isInsideSolidGeometry)
        {
            currentSpeed *= insideGeometrySpeedMultiplier;
        }

        root.position += dirToTarget * currentSpeed * Time.deltaTime;
        isMoving = true;
        wantsToMove = true;
        PlayAnim("Move", 0.25f);
    }
    public void StopMoving()
    {
        PlayAnim("Idle", 0.1f);
        isMoving = false;
    }
    public float GetHorizontalAngleToTarget(Transform target)
    {
        // 1. Get direction to target and flatten it to the horizontal plane (ignore height difference)
        Vector3 dirToTarget = target.transform.position - root.position;
        dirToTarget.y = 0f;

        // 2. Get the NPCs actual horizontal forward direction
        float yaw = root.eulerAngles.y;
        Vector3 horizontalForward = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

        // 3. Calculate the signed angle around the global up axis (y)
        return Vector3.SignedAngle(horizontalForward, dirToTarget, Vector3.up);
    }
    public void TurnToLookAt(Transform target)
    {
        if (target == null) return;

        float angle = GetHorizontalAngleToTarget(target);
        //Debug.Log(angle);

        bool onTarget = Mathf.Abs(angle) < 2f;
        if (!onTarget)
        {
            if (angle < 0f) TurnLeft();
            else TurnRight();
        }
        isLookingAtNavTarget = onTarget;
    }

    private void TurnLeft()
    {
        transform.Rotate(new Vector3(0, 0, -turnSpeed * Time.deltaTime), Space.Self);
    }

    private void TurnRight()
    {
        transform.Rotate(new Vector3(0, 0, turnSpeed * Time.deltaTime), Space.Self);
    }

    private void PlayAnim(string stateName, float blendTime = 0f, bool forceRestart = false)
    {
        int targetHash = Animator.StringToHash(stateName);

        if (currentAnimHash != targetHash || forceRestart)
        {
            if (blendTime > 0f)
                animator.CrossFade(targetHash, blendTime, 0, 0f);
            else
                animator.Play(targetHash, 0, 0f);

            currentAnimHash = targetHash;
        }
    }
    #endregion
}