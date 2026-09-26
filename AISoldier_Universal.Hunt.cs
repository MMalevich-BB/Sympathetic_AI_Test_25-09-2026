using UnityEngine;

public partial class AISoldier_Universal
{
    [Header("Hunt")]
    [SerializeField] private float huntTrackingDuration = 5f;
    [SerializeField] private float huntTrackingInterval = 0.5f;

    private float huntTrackingTimer = 0f;
    private float huntTrackingIntervalTimer = 0f;
    private bool huntArrivedAtLastKnown = false;

    private float sightHuntTimer = 0f;

    private void UpdateHunt()
    {
        if (CheckLineOfSight())
        {
            Vector3 dirToTarget = myTarget.transform.position - root.transform.position;
            float distToTarget = dirToTarget.magnitude;

            if (distToTarget <= sightRange)
            {
                sightHuntTimer = 0f;
                ChangeState(SoldierStates.Attack);
                return;
            }
        }

        sightHuntTimer += Time.deltaTime;
        if (sightHuntTimer >= sightLossTime)
        {
            sightHuntTimer = 0f;
            ChangeState(SoldierStates.Patrol);
            navReachedTargetNode = true;
            return;
        }

        // Phase 1: re-sample the player's LIVE position and re-path, for the first huntTrackingDuration secs after losing sight
        if (huntTrackingTimer < huntTrackingDuration)
        {
            huntTrackingTimer += Time.deltaTime;
            huntTrackingIntervalTimer += Time.deltaTime;

            if (huntTrackingIntervalTimer >= huntTrackingInterval)
            {
                huntTrackingIntervalTimer = 0f;
                BeginPathToPosition(myTarget.transform.position);
            }

            FollowPath();
            return;
        }

        // Phase 2: tracking window closed. Finish walking whatever path we already had, then hold position and wait for timer
        if (!navReachedTargetNode)
        {
            FollowPath();
            return;
        }

        if (!huntArrivedAtLastKnown)
        {
            SyncArrivalNode();
            StopMoving();
            huntArrivedAtLastKnown = true;
        }
    }

    private void ResetHuntState()
    {
        huntTrackingTimer = 0f;
        huntTrackingIntervalTimer = 0f;
        huntArrivedAtLastKnown = false;
    }
}