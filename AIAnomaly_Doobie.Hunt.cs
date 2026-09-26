using UnityEngine;
using System.Collections.Generic;

public partial class AIAnomaly_Doobie
{
    // ==============================
    // TELEPORT PHASE TRACKING
    // ==============================
    private enum TeleportPhase { None, Disappearing, Hidden, Appearing }
    private TeleportPhase teleportPhase = TeleportPhase.None;

    // ==============================
    // MAIN HUNT UPDATE
    // ==============================
    [SerializeField] private float attackInterval = 2f;
    private float attackIntervalTimer = 0f;
    [SerializeField] private float attackForce = 2f;
    [SerializeField] private PlayerController player;
    private void UpdateHunt()
    {
        attackIntervalTimer += Time.deltaTime;

        if (myTarget == null)
        {
            StopMoving();
            ChangeState(DoobieStates.Patrol);
            return;
        }

        // tick down the teleport cooldown regardless of what else is happening
        if (teleportCooldownTimer > 0f)
            teleportCooldownTimer -= Time.deltaTime;

        // if we are mid-teleport, run the teleport sequence and skip all chase logic
        if (teleportPhase != TeleportPhase.None)
        {
            HandleTeleportSequence();
            return;
        }

        // 1. Calc horizontal dist to player
        Vector3 dirToPlayer = myTarget.transform.position - root.position;
        dirToPlayer.y = 0f;
        float distToPlayer = dirToPlayer.magnitude;

        // 2. CONTACT CHECK
        if (distToPlayer <= huntCatchDistance)
        {
            CatchPlayer();
            return;
        }

        // 3. TELEPORT CHECK: player is near the edge of chase range and looking away
        if (ShouldAttemptTeleport(distToPlayer))
        {
            StartTeleport();
            return;
        }

        // 4. IN RANGE: player is within the chase radius
        if (distToPlayer <= sightRange)
        {
            currentLossTimer = 0f;
            lastKnownPlayerPos = myTarget.transform.position;
            Move(myTarget.transform);
            return;
        }

        // 5. OUT OF RANGE: player escaped the chase radius
        currentLossTimer += Time.deltaTime;

        if (currentLossTimer >= sightLossTime)
        {
            StopMoving();
            ChangeState(DoobieStates.Patrol);
            return;
        }

        // Chase the last known pos
        Vector3 flatDir = lastKnownPlayerPos - root.position;
        flatDir.y = 0f;
        float flatDist = flatDir.magnitude;

        if (flatDist > 0.5f)
        {
            MoveTowardsPosition(lastKnownPlayerPos);
        }
        else
        {
            StopMoving();
        }
    }

    // ==============================
    // TELEPORT DECISION
    // ==============================

    /// <summary>
    /// Determines whether Doobie should attempt a blink teleport. Conditions: player is within ±3m of the chase range boundary, the player is NOT looking at Doobie, and the cooldown has elapsed.
    /// </summary>
    private bool ShouldAttemptTeleport(float distToPlayer)
    {
        // Cooldown gate
        if (teleportCooldownTimer > 0f) return false;

        // Dist gate: player must be near the edge of the chase range
        float triggerMin = sightRange - teleportTriggerRange;  // 10 - 3 = 7m
        float triggerMax = sightRange + teleportTriggerRange;  // 10 + 3 = 13m

        if (distToPlayer < triggerMin || distToPlayer > triggerMax)
            return false;

        // Obs gate: player must NOT be looking at doobie
        if (IsPlayerLookingAtMe()) return false;

        return true;
    }

    // ==============================
    // TELEPORT SEQUENCE
    // ==============================

    private void StartTeleport()
    {
        teleportPhase = TeleportPhase.Disappearing;
        StopMoving();
        PlayAnim("Disappear", 0.15f, forceRestart: true);
        randomSoundTimer = 0f;
        PlayTeleportDisappearSound();
    }

    private void HandleTeleportSequence()
    {
        switch (teleportPhase)
        {
            case TeleportPhase.Disappearing:
                // Wait until the animator finishes "Disappear" and enters "Hidden"
                if (animator.GetCurrentAnimatorStateInfo(0).IsName("Hidden"))
                {
                    // tp
                    TeleportToNodeNearPlayer();
                    teleportPhase = TeleportPhase.Hidden;
                }
                break;

            case TeleportPhase.Hidden:
                // Doobie is underground at new loc
                PlayAnim("Appear", 0.15f, forceRestart: true);
                PlayTeleportAppearSound();
                randomSoundTimer = 0f;
                teleportPhase = TeleportPhase.Appearing;
                break;

            case TeleportPhase.Appearing:
                // Wait until "Appear" finishes and the animator leaves the Appear/Hidden states
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName("Appear") && !info.IsName("Hidden"))
                {
                    // finished!
                    teleportPhase = TeleportPhase.None;
                    teleportCooldownTimer = teleportCooldown;
                }
                break;
        }
    }

    /// <summary>
    /// Finds a random navigation node within teleportSearchRadius of the player
    /// and snaps Doobie's root position to it.
    /// </summary>
    private void TeleportToNodeNearPlayer()
    {
        if (cachedNodes == null) CacheNodes();

        List<NavigationNode> nearbyNodes = new List<NavigationNode>();

        foreach (var node in cachedNodes)
        {
            if (node == null) continue;
            float dist = Vector3.Distance(node.transform.position, myTarget.transform.position);
            if (dist <= teleportSearchRadius)
            {
                nearbyNodes.Add(node);
            }
        }

        if (nearbyNodes.Count > 0)
        {
            NavigationNode chosen = nearbyNodes[Random.Range(0, nearbyNodes.Count)];
            root.position = chosen.transform.position;
        }
        // If no nodes are nearby, Doobie stays in place and just reappears. This is a safe fallback so Doobie never gets stuck underground.
    }

    // ==============================
    // CATCH PLAYER
    // ==============================

    private void CatchPlayer()
    {
        StopMoving();

        if (attackIntervalTimer >= attackInterval)
        {
            player.TakeDamageMelee(0, 0, 0.01f, attackForce, 2f, true);
            attackIntervalTimer = 0f;
            PlayRandomSoundFromList(audioAttack, audioBody, true);
        }

        //Debug.Log("[Hunt] Doobie caught the player!");
    }
}