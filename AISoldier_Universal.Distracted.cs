using UnityEngine;

public partial class AISoldier_Universal
{
    // TODO: ADD PROPER TIMER; SOLDIER IMMEDIATELY GOES TO HUNT AFTER THIS
    private float targetDeliberationTime = 0f;
    private void UpdateDistracted()
    {
        //TODO: ADD DISTRACTED ANIMATIONS

        if (targetDeliberationTime == 0f || targetDeliberationTime == 0)
            RollDeliberationTime(); //init

        if (CheckLineOfSight())
        {
            Vector3 dirToTarget = myTarget.transform.position - root.transform.position;
            float distToTarget = dirToTarget.magnitude;

            if (distToTarget < 0.5 * sightRange) ChangeState(SoldierStates.Attack);
        }

        disDeliberationTimer += Time.deltaTime;

        if (disDeliberationTimer >= targetDeliberationTime)
        {
            if (CheckLineOfSight()) ChangeState(SoldierStates.Hunt);
            else if (UnityEngine.Random.Range(0f, 1f) <= disToIdleChance) ChangeState(SoldierStates.Patrol);
                else ChangeState(SoldierStates.Investigate);
        }

    }

    private void RollDeliberationTime()
    {
        targetDeliberationTime = UnityEngine.Random.Range(disDeliberationTimeMin, disDeliberationTimeMax);
    }
}