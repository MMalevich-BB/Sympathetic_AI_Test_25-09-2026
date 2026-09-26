using UnityEngine;

public partial class AISoldier_Universal
{
    [Header("Investigate")]
    [SerializeField] private float investigateWaitDuration = 8f;
    [SerializeField] private float investigateMaxDuration = 15f; // safety cap if the path never solevs

    private bool investigateDestinationSet = false;
    private bool investigateWaiting = false;
    private float investigateWaitTimer = 0f;
    private float investigateElapsed = 0f;

    private void UpdateInvestigate()
    {
        if (CheckLineOfSight())
        {
            ChangeState(SoldierStates.Distracted);
            moveState = AIMoveState.Normal;
            return;
        }

        investigateElapsed += Time.deltaTime;
        if (investigateElapsed >= investigateMaxDuration)
        {
            ChangeState(SoldierStates.Patrol);
            moveState = AIMoveState.Normal;
            return;
        }

        if (!investigateDestinationSet)
        {
            BeginPathToPosition(lastTargetPos);
            moveState = AIMoveState.Slow;
            investigateDestinationSet = true;
            return;
        }

        if (!navReachedTargetNode)
        {
            FollowPath();
            return;
        }

        if (!investigateWaiting)
        {
            SyncArrivalNode();
            StopMoving();
            investigateWaiting = true;
            investigateWaitTimer = 0f;
        }

        investigateWaitTimer += Time.deltaTime;
        if (investigateWaitTimer >= investigateWaitDuration)
        {
            moveState = AIMoveState.Normal;
            ChangeState(SoldierStates.Patrol);
        }
    }

    private void ResetInvestigateState()
    {
        investigateDestinationSet = false;
        investigateWaiting = false;
        investigateWaitTimer = 0f;
        investigateElapsed = 0f;
    }
}