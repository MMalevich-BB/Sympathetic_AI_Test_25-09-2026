using UnityEngine;

public partial class AISoldier_Universal
{
    [Header("Attack")]
    [SerializeField] private bool isAlert;
    [SerializeField] private float sightAlertTimeout = 2f;
    private float alertTimer = 0f;

    [SerializeField] private MuzzleFlash muzzle;
    [SerializeField] private AudioSource audioMuzzle;
    [SerializeField] private GameObject barrel;
    [SerializeField] private float gunshotInterval = 0.12f;
    [SerializeField] private float attackDelay = 0.75f;
    [SerializeField] private float attackFOV = 10f;
    private float attackDelayTimer = 0f;
    private float gunshotTimer = 0f;
    private bool canAttack = false;
    private bool wasCanAttackLastFrame = false;

    [SerializeField] private GameObject bulletImpactPrefab;
    private void UpdateAttack()
    {
        HandleAimAnim();
        HandleTrackingLogic();
        HandleAttack();
    }

    private void HandleAimAnim()
    {
        bool hasLOS = CheckLineOfSight();
        bool shootLOS = CheckTargetInAttackAngle();

        if (!isAiming && hasLOS)
        {
            isAiming = true;
            UpdateAimAnimState();
            canAttack = shootLOS;
        }

        if (isAiming && hasLOS)
        {
            isAlert = false;
            alertTimer = 0f;
            canAttack = shootLOS;
        }

        if (isAiming && !hasLOS && !isAlert)
        {
            isAlert = true;
            alertTimer = 0f;
            canAttack = false;
        }

        if (isAiming && !hasLOS && isAlert)
        {
            alertTimer += Time.deltaTime;
            canAttack = false;
            if (alertTimer > sightAlertTimeout)
            {
                isAlert = false;
                isAiming = false;
                canAttack = false;
                UpdateAimAnimState();
                ChangeState(SoldierStates.Hunt);
            }
        }
    }
    private void UpdateAimAnimState() { animator.SetBool("IsAiming", isAiming); }
    private void HandleTrackingLogic()
    {
        if (isAiming)
        {
            currentTurnAngle = GetHorizontalAngleToTarget();
            float clampedAngle = Mathf.Clamp(currentTurnAngle, -45f, 45f);
            animator.SetFloat("AimAngle", clampedAngle, 0.25f, Time.deltaTime);
        }
        
    }
    private bool CheckTargetInAttackAngle()
    {
        if (Mathf.Abs(currentTurnAngle) < attackFOV) return true;
        else return false;
    }

    private void HandleAttack()
    {
        // Detect the moment the soldier is able to attack
        if (canAttack && !wasCanAttackLastFrame)
        {
            attackDelayTimer = 0f;
            gunshotTimer = 0f;
        }
        wasCanAttackLastFrame = canAttack;

        if (!canAttack) return;

        if (attackDelayTimer >= attackDelay)
        {
            // FIRE!!!
            if (gunshotTimer >= gunshotInterval)
            {
                muzzle.Fire();
                PlayGunSound(GunSounds.GunFire, 1f);
                AttackTrace();
                gunshotTimer = 0f;
            }
            else
            {
                gunshotTimer += Time.deltaTime;
            }
        }
        else
        {
            attackDelayTimer += Time.deltaTime;
        }
    }

    private void AttackTrace()
    {
        // DAWG WE BE SHOOTING AT SHIT
        // Originally was chance-based trace to the player a la Containment Breach, idea dropped
        bool hit = Physics.Raycast(
            barrel.transform.position,
            barrel.transform.forward,
            out RaycastHit hitInfo,
            50f,
            sightLayerMask
            );
        if (hit) {
            SurfaceType surfaceType = SurfaceType.Default;
            SurfaceIdentity surface = hitInfo.transform.gameObject.GetComponent<SurfaceIdentity>();
            if (surface != null) { surfaceType = surface.SurfaceType; }

            PlayerController player = hitInfo.transform.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamageBullet();
                surfaceType = SurfaceType.Flesh;
                CreateBulletImpactSound(hitInfo.point, bulletImpactPrefab, surfaceType);
            }
            else
            CreateBulletImpactSound(hitInfo.point, bulletImpactPrefab, surfaceType);

        }
    }

    private void CreateBulletImpactSound(Vector3 pos, GameObject prefab, SurfaceType material = SurfaceType.Default)
    {
        if (prefab != null)
        {
            BulletImpactSounder bis = GameObject.Instantiate(prefab, pos, Quaternion.identity).
                                        GetComponent<BulletImpactSounder>();

            bis.surfaceType = material;
        }
    }

    public void FireSequence()
    {
        muzzle.Fire();
        PlayGunSound(GunSounds.GunFire, 1f);
        AttackTrace();
    }
}
