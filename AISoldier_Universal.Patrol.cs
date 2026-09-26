using UnityEngine;
using System.Collections.Generic;

public partial class AISoldier_Universal
{
    // ==============================
    // PATROL STATE VARIABLES
    // ==============================
    private bool isWaitingAtNode = false;
    private float waitTimer = 0f;
    private int currentPathIndex = 0;
    private bool reachedCurrentWaypoint = false;
    private string pendingTransitionAnim = null;

    // Tracks where the AI came from to prevent bouncing back and forth on two-way links
    private NavigationNode previousNode;

    // ==============================
    // MAIN PATROL UPDATE
    // ==============================
    private void UpdatePatrol()
    {
        if (CheckLineOfSight())
        {
            ChangeState(SoldierStates.Distracted);
            StopMoving();
            return;
        }
        // 1. INIT: If the soldier spawned without a target, find the nearest node and go to it
        if (navCurrentNode == null)
        {
            GameObject nearest = FindNearestNode(root.position);
            if (nearest != null)
            {
                navCurrentNode = nearest;
                navTargetNode = nearest;
                navReachedTargetNode = true; // Forces the loop to jump to step 3
            }
            return;
        }

        // 2. WAITING:
        if (isWaitingAtNode)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaitingAtNode = false;

                if (string.IsNullOrEmpty(pendingTransitionAnim))
                {
                    // Regular wait
                    // Return to idle, pick a new destination
                    PlayAnim("Idle", 0.2f);
                    PickNextNodeAndMove();
                }
                pendingTransitionAnim = null;
            }
            return;
        }

        // 3. ARRIVAL:
        if (navReachedTargetNode)
        {
            ArriveAtNode();
            return;
        }

        // 4. TRAVEL:
        FollowPath();
    }

    // ==============================
    // PATROL PHASE METHODS
    // ==============================

    private void FollowPath()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count) return;

        reachedCurrentWaypoint = false;
        Move(currentPath[currentPathIndex]);

        if (reachedCurrentWaypoint)
        {
            currentPathIndex++;

            if (currentPathIndex >= currentPath.Count)
                navReachedTargetNode = true;
        }
    }

    private void ArriveAtNode()
    {
        StopMoving();
        SyncArrivalNode();

        NavigationNode currentComp = navCurrentNode.GetComponent<NavigationNode>();

        // Context anims are handled in PickNextNodeAndMove() via transitionSequences (inside each node, check NavigationNode_Script.cs)
        if (currentComp.waitTime > 0f)
        {
            isWaitingAtNode = true;
            waitTimer = currentComp.waitTime;
            PlayAnim("Idle", 0.2f);
        }
        else
        {
            PickNextNodeAndMove();
        }
    }

    private void PickNextNodeAndMove()
    {
        if (navCurrentNode == null) return;

        NavigationNode currentComp = navCurrentNode.GetComponent<NavigationNode>();
        NavigationNode nextDestination = null;

        // 1. Check if node has hard-set next node
        if (currentComp.nextNode != null)
        {
            nextDestination = currentComp.nextNode;
        }
        // 2. Fallback: pick from the neighbours array
        else if (currentComp.neighbours != null && currentComp.neighbours.Length > 0)
        {
            if (currentComp.neighbours.Length == 1)
            {
                // Dead end or single path thing
                nextDestination = currentComp.neighbours[0];
            }
            else
            {
                // Smart select
                List<NavigationNode> validNeighbours = new List<NavigationNode>();
                foreach (var n in currentComp.neighbours)
                {
                    if (n != null && n != previousNode)
                    {
                        validNeighbours.Add(n);
                    }
                }

                if (validNeighbours.Count > 0)
                {
                    nextDestination = validNeighbours[Random.Range(0, validNeighbours.Count)];
                }
                else
                {
                    nextDestination = currentComp.neighbours[Random.Range(0, currentComp.neighbours.Length)];
                }
            }
        }

        if (nextDestination != null)
        {
            // ===== Context transition sequence =====
            bool playedTransition = false;

            NodeTransitionSequence seq = currentComp.GetTransitionTo(nextDestination, moveState);
            if (seq != null)
            {
                string anim = currentComp.PickTransitionAnimation(seq);
                if (!string.IsNullOrEmpty(anim))
                {
                    isWaitingAtNode = true;
                    pendingTransitionAnim = anim;
                    PlayAnim(anim, 0.1f);
                    waitTimer = animClipLengths.TryGetValue(anim, out float len) ? len : 1f; // fallback if not found
                    playedTransition = true;
                }
            }
            // ===== End context sequence =====

            navTargetNode = nextDestination.gameObject;
            SetPathTo(nextDestination);
        }
        else
        {
            Debug.LogWarning($"[Black Ops: Patrol] Node {navCurrentNode.name} has no nextNode and no neighbours. Soldier stuck.");
        }
    }

    // ==============================
    // HELPER: FIND NEAREST NODE
    // ==============================

    private GameObject FindNearestNode(Vector3 origin)
    {
        float lowestDist = Mathf.Infinity;
        GameObject chosenNode = null;

        NavigationNode[] nodegraph = FindObjectsByType<NavigationNode>();

        foreach (NavigationNode node in nodegraph)
        {
            float dist = (node.transform.position - origin).magnitude;
            if (dist < lowestDist)
            {
                lowestDist = dist;
                chosenNode = node.gameObject;
            }
        }
        return chosenNode;
    }
}