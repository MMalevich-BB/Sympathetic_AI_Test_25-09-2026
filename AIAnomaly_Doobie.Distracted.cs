using UnityEngine;

public partial class AIAnomaly_Doobie
{
    // Internal phase trackers
    private bool isDeliberating = false;
    private bool isInvestigating = false;
    private float disDeliberationTargetTime = 0f;
    private float investigateWaitTimer = 0f;

    private void UpdateDistracted()
    {
        if (myTarget == null)
        {
            ChangeState(DoobieStates.Patrol);
            return;
        }

        // 1. INIT (First frame)
        if (!isDeliberating && !isInvestigating)
        {
            lastKnownPlayerPos = myTarget.transform.position;
            disDeliberationTargetTime = Random.Range(disDeliberationTimeMin, disDeliberationTimeMax);
            disDeliberationTimer = 0f;
            investigateWaitTimer = 0f;
            isDeliberating = true;
            PlayDistractedSound();
            randomSoundTimer = 0f;
            StopMoving(); // Stand still while deliberating
        }

        // 2. DELIBERATION PHASE 
        // NO LOS CHECK HERE. The anomaly is "processing" the distraction 
        // the player has this entire window to hide
        // (need to implement this for the soldiers as well)

        if (isDeliberating)
        {
            TurnToLookAtPosition(lastKnownPlayerPos);
            disDeliberationTimer += Time.deltaTime;

            if (disDeliberationTimer >= disDeliberationTargetTime)
            {
                isDeliberating = false;

                // --- INTERRUPTER 1: ---
                // the anomaly finishes. is the player STILL visible?
                if (CheckLineOfSight())
                {
                    ResetDistractedFlags();
                    PlayAlertSound();
                    randomSoundTimer = 0f;
                    ChangeState(DoobieStates.Hunt);
                    return;
                }

                // player hid in time, roll the dice.
                if (Random.value < disToIdleChance)
                {
                    ResetDistractedFlags();
                    ChangeState(previousState);
                }
                else
                {
                    // 60% chance to go investigate the spot
                    isInvestigating = true;
                }
            }
            return;
        }

        // 3. INVESTIGATION PHASE
        if (isInvestigating)
        {
            // --- INTERRUPTER 2: ---
            // while gliding toward the spot, if the player peeks out, snap to Hunt
            if (CheckLineOfSight())
            {
                ResetDistractedFlags();
                PlayAlertSound();
                randomSoundTimer = 0f;
                ChangeState(DoobieStates.Hunt);
                return;
            }

            Vector3 flatDir = lastKnownPlayerPos - root.position;
            flatDir.y = 0f; // ignore height difference
            float flatDist = flatDir.magnitude;

            if (flatDist > 0.5f) // arrival threshold
            {
                MoveTowardsPosition(lastKnownPlayerPos);
            }
            else
            {
                // arrived at spot
                StopMoving();
                investigateWaitTimer += Time.deltaTime;

                // wait 5 seconds staring at the empty spot, then give up
                if (investigateWaitTimer > 5f)
                {
                    ResetDistractedFlags();
                    PlayLossSound();
                    randomSoundTimer = 0f;
                    ChangeState(DoobieStates.Patrol);
                }
            }
            return;
        }
    }

    private void ResetDistractedFlags()
    {
        isDeliberating = false;
        isInvestigating = false;
        disDeliberationTimer = 0f;
        investigateWaitTimer = 0f;
    }

    // ==============================
    // DISTRACTED-SPECIFIC HELPERS
    // ==============================

    /// <summary>
    /// Turns the entity to face a Vector3 coordinate in the world.
    /// </summary>
    private void TurnToLookAtPosition(Vector3 targetPos)
    {
        Vector3 dir = targetPos - root.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        float yaw = root.eulerAngles.y;
        Vector3 horizontalForward = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        float angle = Vector3.SignedAngle(horizontalForward, dir, Vector3.up);

        bool onTarget = Mathf.Abs(angle) < 2f;
        if (!onTarget)
        {
            if (angle < 0f) TurnLeft();
            else TurnRight();
        }
    }

    /// <summary>
    /// Moves the entity towards a Vector3 coordinate, phasing through walls.
    /// </summary>
    private void MoveTowardsPosition(Vector3 targetPos)
    {
        TurnToLookAtPosition(targetPos);

        Vector3 dir = targetPos - root.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        dir.Normalize();
        root.position += dir * moveSpeed * Time.deltaTime;

        isMoving = true;
        PlayAnim("Move", 0.25f);
    }
}