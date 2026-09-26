using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AISoldier_Universal : MonoBehaviour
{
    public SoldierStates currentState = SoldierStates.Idle;
    // All fields subject to population and editing
    // MASTER FILE

    //==============================
    //REFERENCES
    //==============================
    [SerializeField] private Transform root;            // faster than this.transform idk
    [SerializeField] private GameObject myEyes;         // where the vision checks originate from
    [SerializeField] private GameObject myTarget;       // usually the player, might change later
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioBody;
    [SerializeField] private AudioSource audioSpeech;
    [SerializeField] private GameObject rightFootIK;    // for raycast purposes with mats and sounds
    [SerializeField] private AudioSource rightFS;
    [SerializeField] private GameObject leftFootIK;     // for raycast purposes with mats and sounds
    [SerializeField] private AudioSource leftFS;

    //==============================
    //MOVEMENT
    //==============================
    // Movement is animation-driven, with root motion
    public AIMoveState moveState = AIMoveState.Normal;
    private bool wantsToMove = false;
    private bool isMoving = false;

    //==============================
    //ANIMATIONS
    //==============================
    private int currentAnimHash = 0;
    private int targetAnimHash = 0;

    //==============================
    //SIGHT
    //==============================
    [SerializeField] private bool sightLOS = false;         // is the target within the vis-cone unobstructed?
    [SerializeField] private float sightFOV = 60f;          // total; gets halved for a calc of vis-cone
    [SerializeField] private float sightRange = 20f;        // raycast distance in metres
    [SerializeField] private float sightDisToHuntTime = 2f;     // for transitioning from Distracted to Hunt
    private float currentAlertTimer = 0f;
    [SerializeField] private float sightLossTime = 30f;     // time until the player is considered lost, used to transition from Hunt to Patrol
    private float currentLossTimer = 0f;
    [SerializeField] private float sightCheckInterval;
    private float currentSightIntervalTimer = 0f;
    [SerializeField] private LayerMask sightLayerMask;  
    [SerializeField] private bool isAiming;                 // when tracking target and/or firing, must be stationary
    [SerializeField] private float currentTurnAngle;        // when tracking target, always relative to the root
    private GameObject mainCamera;


    //==============================
    //SIGHT -- Distracted and target disappeared
    //==============================
    //Makes a decision in a time range
    [SerializeField] private float disDeliberationTimeMin = 0.5f;
    [SerializeField] private float disDeliberationTimeMax = 3.5f;
    private float disDeliberationTimer = 0f;
    [SerializeField] private float disToIdleChance = 0.35f; //else transitions to Investigate

    //==============================
    //NAVIGATION - Node-based
    //==============================
    [SerializeField] private GameObject navCurrentNode;
    public GameObject navTargetNode;
    [SerializeField] private bool navReachedTargetNode;
    [SerializeField] private Vector3 lastTargetPos;
    [SerializeField] public List<NavigationNode> currentPath = new List<NavigationNode>();
    private bool isLookingAtNavTarget;
    private NavigationNode lastNavTarget;
    //TODO: IMPLEMENT, LATER

    //==============================
    //AUDIO
    //==============================
    //Refer to AISoldier_Universal.Audio.cs -- worked on later down the line
    //Contains material detection raycast logic as well as various audio-related fields
    //Contains hearing logic for the AI

    //==============================
    //SPEECH
    //==============================
    //Refer to AISoldier_Universal.Speech.cs -- worked on later down the line


    private Dictionary<string, float> animClipLengths = new Dictionary<string, float>();






    private void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rightFS == null) rightFS = rightFootIK.GetComponent<AudioSource>();
        if (leftFS == null) leftFS = leftFootIK.GetComponent<AudioSource>();

        mainCamera = Camera.main.gameObject;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (!animClipLengths.ContainsKey(clip.name))
                animClipLengths.Add(clip.name, clip.length);
        }
    }

    void Update()
    {
        // Keep last target pos regardless of state -- Investigate uses this to know where to search, might change it later when vision timer is introduced
        if (CheckLineOfSight())
        {
            lastTargetPos = myTarget.transform.position;
        }

        switch (currentState)
        {
            case SoldierStates.Idle: UpdateIdle(); break;
            case SoldierStates.Patrol: UpdatePatrol(); break;
            case SoldierStates.Distracted: UpdateDistracted(); break;
            case SoldierStates.Investigate: UpdateInvestigate(); break;
            case SoldierStates.Hunt: UpdateHunt(); break;
            case SoldierStates.Attack: UpdateAttack(); break;
            case SoldierStates.Sequence: UpdateSequence(); break;
        }

        UpdateDebug();
        UpdateAudio();
        UpdateSpeech();
    }

    public void ChangeState(SoldierStates newState)
    {
        if (currentState == newState) return;

        switch (currentState)
        {
            case SoldierStates.Investigate:
                ResetInvestigateState();
                break;
            case SoldierStates.Hunt:
                ResetHuntState();
                moveState = AIMoveState.Normal;
                break;
        }

        currentState = newState;

        if (newState == SoldierStates.Hunt)
        {
            moveState = AIMoveState.Fast;
            BeginPathToPosition(myTarget.transform.position);
        }
        else if (newState == SoldierStates.Attack)
        {
            StopMoving();
            isLookingAtNavTarget = false;
        }
    }

    /// <summary>
    /// Finds the nearest NavigationNode to a world position and starts a full multi-hop path toward it. Used by Investigate/Hunt, which -- unlike Patrol -- path to a specific point rather than to an immediate neighbour
    /// </summary>
    private void BeginPathToPosition(Vector3 worldPos)
    {
        // Always re-snap to the node nearest our TRUE current position first. Repeated re-pathing (Hunt's tracking loop) needs this -- otherwise the A* search keeps starting from a stale node, and Move() still walks a straight line to whatever the wrong path says comes next
        GameObject nearestToSelf = FindNearestNode(root.position);
        if (nearestToSelf == null)
        {
            Debug.LogWarning("[Black Ops AI] No navigation nodes found in the scene.");
            return;
        }
        navCurrentNode = nearestToSelf;

        GameObject nearestToTarget = FindNearestNode(worldPos);
        if (nearestToTarget == null)
        {
            Debug.LogWarning("[Black Ops AI] No navigation nodes found in the scene to path towards.");
            return;
        }

        navTargetNode = nearestToTarget;
        SetPathTo(nearestToTarget.GetComponent<NavigationNode>());
    }

    /// <summary>
    /// Shared across states: call once a path's destination is reached, to keep navCurrentNode in sync. Patrol additionally layers its own wait/transition logic on top via ArriveAtNode(); Investigate or Hunt skip that.
    /// </summary>
    private void SyncArrivalNode()
    {
        previousNode = navCurrentNode?.GetComponent<NavigationNode>();
        navCurrentNode = navTargetNode;
        navReachedTargetNode = false;
    }

    /// <summary>
    /// Checks if the target is within the FOV cone and unobstructed by walls. Updates the sightLOS boolean and returns the result.
    /// </summary>
    /*
    public bool CheckLineOfSight()
    {
        // 1. Calculate the target's "chest" position (origin + 1.6m up)
        Vector3 targetChest = myTarget.transform.position + Vector3.up * 1.6f;
        Vector3 dirToTarget = targetChest - myEyes.transform.position;
        float distanceToTarget = dirToTarget.magnitude;

        // 2. Immediate return: Is it too far away?
        if (distanceToTarget > sightRange) return false;

        // 3. Immediate return: Is it outside the FOV cone?
        // Vector3.Angle returns a positive value (0 to 180), so checking against half the FOV 
        // perfectly covers both the positive and negative sides of the cone.
        float angleToTarget = Vector3.Angle(myEyes.transform.forward, dirToTarget);
        if (angleToTarget > sightFOV / 2f) return false;

        // 4. Raycast check: Is there a wall in the way?
        Ray sightRay = new Ray(myEyes.transform.position, dirToTarget.normalized);

        if (Physics.Raycast(sightRay, out RaycastHit hit, distanceToTarget, sightLayerMask))
        {
            // If we hit the target or a child of the target (like a mesh), set LOS to true
            if (hit.transform == myTarget.transform || hit.transform.IsChildOf(myTarget.transform))
            {
                return true;
            }
        }

        // If we hit a wall, or the ray didn't hit the target, LOS is blocked.
        return false;
    }
    */
    public bool CheckLineOfSight()
    {
        // 1.If Main Camera doesn't exist or is somehow not assigned, calculate the target's "chest" pos (origin + 1.4m up?)
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

        // 2. Immediate return: is it too far away?
        if (distanceToTarget > sightRange)
        {
            // Draw a RED line
            Debug.DrawLine(myEyes.transform.position, myEyes.transform.position + dirToTarget, Color.red);
            return false;
        }

        // 3. Immediate return: is it outside the FOV cone?
        float angleToTarget = Vector3.Angle(myEyes.transform.forward, dirToTarget);
        if (angleToTarget > sightFOV / 2f)
        {
            // Draw a YELLOW line
            Debug.DrawLine(myEyes.transform.position, myEyes.transform.position + dirToTarget, Color.yellow);
            return false;
        }

        // 4. Raycast check: is there a wall in the way?
        Ray sightRay = new Ray(myEyes.transform.position, dirToTarget.normalized);

        // OLD:
        // if (Physics.Raycast(sightRay, out RaycastHit hit, distanceToTarget, sightLayerMask))

        // NEW:
        if (Physics.SphereCast(sightRay, 0.2f, out RaycastHit hit, distanceToTarget, sightLayerMask))
        {
            // Draw a GREEN line
            Debug.DrawLine(myEyes.transform.position, hit.point, Color.green);

            if (hit.transform == myTarget.transform || hit.transform.IsChildOf(myTarget.transform))
            {
                return true;
            }
            else
            {
                // Draw a PURPLE line
                Debug.DrawRay(hit.point, Vector3.up * 0.5f, Color.magenta, 0.1f);
            }
        }
        else
        {
            // No hit
            // Draw a CYAN line
            Debug.DrawLine(myEyes.transform.position, myEyes.transform.position + dirToTarget, Color.cyan);
        }

        return false;
    }

    /// <summary>
    /// Calculates the horizontal (Yaw) signed angle from the NPC to the target. Returns a value between -180 and 180.  Positive means the target is to the right, Negative means left.
    /// </summary>
    public float GetHorizontalAngleToTarget()
    {
        // 1. Get direction to target and flatten it to the horizontal plane (ignore height difference)
        Vector3 dirToTarget = myTarget.transform.position - root.position;
        dirToTarget.y = 0f;

        // 2. Get the NPC's actual horizontal forward direction.
        float yaw = root.eulerAngles.y;
        Vector3 horizontalForward = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

        // 3. Calculate the signed angle around the global Up axis (Y)
        return Vector3.SignedAngle(horizontalForward, dirToTarget, Vector3.up);
    }



    /// <summary>
    /// Recalcs currentPath from currentNode to the given target node. Call this whenever the NPC needs a new route (on spawn, on new goal, etc.). Public, can be called by script, useful for sequences and etc.
    /// </summary>
    public void SetPathTo(NavigationNode target)
    {
        currentPath = NavigationPathfinder.FindPath(navCurrentNode.GetComponent<NavigationNode>(), target);

        // Index 0 is always our own current node (already there) -- start at the
        // first *real* waypoint, or FollowPath() detects a false zero-distance
        // "arrival" before any actual movement happens.
        currentPathIndex = currentPath.Count > 1 ? 1 : 0;
    }



    /// <summary>
    /// Turn and move towards the target linearly.
    /// </summary>
    public void Move(Transform target)
    {
        if (
            isAiming ||
            currentState == SoldierStates.Attack ||
            currentState == SoldierStates.Distracted ||
            currentState == SoldierStates.Sequence ||
            currentState == SoldierStates.Idle
            ) return;
        if (target == null) return;

        if (isLookingAtNavTarget)
        {
            float distToTarget = (target.position - root.position).magnitude;

            // FIX: Added arrival handle for Transform targets
            if (distToTarget > 1.5f)
            {
                Walk();
            }
            else
            {
                StopMoving();
            }
        }
        else TurnToLookAt(target);
    }
    public void Move(NavigationNode target, bool allowStopDelay = false)
    {
        if (
            isAiming ||
            currentState == SoldierStates.Attack ||
            currentState == SoldierStates.Distracted ||
            currentState == SoldierStates.Sequence ||
            currentState == SoldierStates.Idle
            ) return;
        if (target == null) return;

        if (target != lastNavTarget)
        {
            isLookingAtNavTarget = false;
            lastNavTarget = target;
        }

        float distToTarget = (target.transform.position - root.position).magnitude;

        // Check arrival BEFORE facing. If root motion (like transition sequence) already carried us onto the node, register arrive immediately instead of falling into TurnToLookAt with a near-zero direction vector
        if (distToTarget <= target.insideRadius)
        {
            if (
                allowStopDelay &&
                currentState != SoldierStates.Attack &&
                currentState != SoldierStates.Distracted &&
                currentState != SoldierStates.Hunt
                )
            {
                float time = UnityEngine.Random.Range(0.1f, 1.0f);
                StopMovingWithRandomDelay(time);
            }
            else
                StopMoving();

            reachedCurrentWaypoint = true;
            return;
        }

        if (isLookingAtNavTarget)
        {
            float currentAngleAbs = Mathf.Abs(GetHorizontalAngleToTarget(target.transform));
            if (currentAngleAbs > 10f)
            {
                isLookingAtNavTarget = false;
                return;
            }
            Walk();
        }
        else TurnToLookAt(target.transform);
    }

    public void Walk()
    {
        string targetAnim = "WalkFwd";
        switch (moveState)
        {
            case AIMoveState.Slow: targetAnim = "WalkFwdSlow"; break;
            case AIMoveState.Normal: targetAnim = "WalkFwd"; break;
            case AIMoveState.Fast: targetAnim = "WalkFwdHurried"; break;
        }
        PlayAnim(targetAnim, 0.05f);
    }

    public void StopMoving()
    {
            PlayAnim("Idle", 0.1f);
    }
    IEnumerator StopMovingWithRandomDelay(float t)
    {
        yield return new WaitForSeconds(t);
        PlayAnim("Idle", 0.1f);
    }

    public float GetHorizontalAngleToTarget(Transform target)
    {
        // 1. Get dir to target and flatten it to the horizontal plane (no height difference)
        Vector3 dirToTarget = target.transform.position - root.position;
        dirToTarget.y = 0f;

        // 2. Get the NPC's actual horizontal forwards direction.
        float yaw = root.eulerAngles.y;
        Vector3 horizontalForward = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

        // 3. Calc the signed angle around the global Up axis (+Y)
        return Vector3.SignedAngle(horizontalForward, dirToTarget, Vector3.up);
    }

    public void TurnToLookAt(Transform target)
    {
        if (isAiming || currentState == SoldierStates.Attack) return;
        if (target == null) return; 

        float angle = GetHorizontalAngleToTarget(target);
        //Debug.Log(angle);

        bool onTarget = Mathf.Abs(angle) < 2f;

        if (!onTarget)
        {
            if (angle < -1f) TurnLeft();
            if (angle > 1f) TurnRight();
        }
        isLookingAtNavTarget = onTarget;
    }

    private void TurnLeft()
    {
        PlayAnim("TurnLeft30dLOOP", 0.05f);
    }

    private void TurnRight()
    {
        PlayAnim("TurnRight30dLOOP", 0.05f);
    }


    /// <summary>
    /// Centralised animation controller (very convenient). USE THIS!
    /// </summary>
    /// <param name="stateName">Name of the Animator State.</param>
    /// <param name="blendTime">Time to blend. 0f = instant snap.</param>
    /// <param name="forceRestart">If true, resets the animation to frame 0 even if it's already playing.</param>
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
}