using UnityEngine;

public partial class AIAnomaly_Doobie
{
    private void UpdateIdle()
    {
        HandleIdleAnim();
        if (CheckLineOfSight())
        {
            ChangeState(DoobieStates.Distracted);
            return;
        }
    }
    private void HandleIdleAnim()
    {
        PlayAnim("Idle");

    }
    private void UpdateSequence()
    {
    }
}
