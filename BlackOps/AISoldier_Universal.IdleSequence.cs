using System;
using UnityEngine;

public partial class AISoldier_Universal
{

    #region Idle State
    private AnimatorStateInfo animatorState;
    private void UpdateIdle()
    {
        HandleIdleAnim();
        if (CheckLineOfSight())
        {
            ChangeState(SoldierStates.Distracted);
            return;
        }
    }

    private void HandleIdleAnim()
    {
        PlayAnim("Idle");

    }
    private void RollIdleAnim()
        // called by animation event at the end of the Idle animation, probably state-dependent
        // update: State-dependent
    {
        if (currentState == SoldierStates.Idle)
        {
            float val = UnityEngine.Random.Range(0.0f, 1.0f);
            if (val > 0.5f)
            {
                if (val > 0.95f)
                {
                    PlayAnim("IdleExtra2"); return;
                }
                if (val > 0.725f)
                {
                    PlayAnim("IdleExtra3"); return;
                }
                PlayAnim("IdleExtra1"); return;
            }
        }

        if (currentState == SoldierStates.Hunt)
        {
            float val = UnityEngine.Random.Range(0.0f, 1.0f);
            if (val > 0.1f)
            {
                if (val > 0.8f)
                {
                    PlayAnim("IdleExtra3"); return;
                }
                if (val > 0.6f)
                {
                    PlayAnim("IdleExtra1"); return;
                }
                PlayAnim("HuntIdleExtra1"); return;
            }
        }
    }
    // TESTED: Works!

    #endregion

    #region Sequence State
    private void UpdateSequence()
    {
        // TODO: Implement
    }
    #endregion
}
