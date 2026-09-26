using UnityEngine;

public partial class AIAnomaly_Doobie
{
    // ==============================
    // PATROL STATE VARIABLES
    // ==============================
    [Header("Patrol")]
    [SerializeField] private float patrolWaitMin = 12f;
    [SerializeField] private float patrolWaitMax = 45f;
    private float patrolWaitTimer = 0f;
    private float patrolWaitTarget = 0f;
    private bool isPatrolWaiting = false;
    private Transform patrolTargetNode = null;
    private NavigationNode[] cachedNodes = null;

    // ==============================
    // MAIN PATROL UPDATE
    // ==============================
    private void UpdatePatrol()
    {
        // 1. SENSORY CHECK: if we see the player, get distracted. Runs during both the wait and the drift.
        if (CheckLineOfSight())
        {
            ChangeState(DoobieStates.Distracted);
            return;
        }

        // 2. cache the node graph once on first entry.
        if (cachedNodes == null) CacheNodes();

        // 3. if we have no destination and are not waiting, begin wait cycle.
        if (patrolTargetNode == null && !isPatrolWaiting)
        {
            StartPatrolWait();
            return;
        }

        // 4. WAITING PHASE: dormant at current position, counting down.
        if (isPatrolWaiting)
        {
            patrolWaitTimer += Time.deltaTime;
            if (patrolWaitTimer >= patrolWaitTarget)
            {
                isPatrolWaiting = false;
                PickPatrolTarget();
            }
            return;
        }

        // 5. MOVING PHASE: drift toward the target node, phasing through walls.
        if (patrolTargetNode != null)
        {
            Vector3 flatDir = patrolTargetNode.position - root.position;
            flatDir.y = 0f;
            float flatDist = flatDir.magnitude;

            if (flatDist > 0.5f) // Arrival threshold
            {
                Move(patrolTargetNode);
            }
            else
            {
                // arrived, clear the target andrepeat.
                patrolTargetNode = null;
                StartPatrolWait();
            }
        }
    }

    // ==============================
    // PATROL PHASE METHODS
    // ==============================

    private void CacheNodes()
    {
        cachedNodes = FindObjectsByType<NavigationNode>();
    }

    private void StartPatrolWait()
    {
        isPatrolWaiting = true;
        patrolWaitTimer = 0f;
        patrolWaitTarget = Random.Range(patrolWaitMin, patrolWaitMax);
        StopMoving();
    }

    /// <summary>
    /// Picks Doobie's next drift destination.
    /// 50% chance: go to the closest node.
    /// 50% chance: go to the node closest to that closest node.
    /// </summary>
    private void PickPatrolTarget()
    {
        if (cachedNodes == null || cachedNodes.Length == 0)
        {
            CacheNodes();
            if (cachedNodes == null || cachedNodes.Length == 0) return;
        }

        // Step 1: find the single closest node to doobie's current position.
        NavigationNode closestNode = null;
        float closestDist = Mathf.Infinity;

        foreach (var node in cachedNodes)
        {
            if (node == null) continue;
            float dist = Vector3.Distance(root.position, node.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestNode = node;
            }
        }

        if (closestNode == null) return;

        // step 2: roll the 50/50.
        if (Random.value < 0.5f)
        {
            // heads: Drift to the closest node.
            patrolTargetNode = closestNode.transform;
        }
        else
        {
            // tails: drift to the node closest to the closest node (excluding itself).
            NavigationNode secondPick = null;
            float secondDist = Mathf.Infinity;

            foreach (var node in cachedNodes)
            {
                if (node == null || node == closestNode) continue;
                float dist = Vector3.Distance(closestNode.transform.position, node.transform.position);
                if (dist < secondDist)
                {
                    secondDist = dist;
                    secondPick = node;
                }
            }

            // fallback: if there is only one node in the scene (WTF), just go to it.
            patrolTargetNode = (secondPick != null) ? secondPick.transform : closestNode.transform;
        }
    }
}