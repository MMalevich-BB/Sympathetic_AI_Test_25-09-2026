using System;
using UnityEngine;

public partial class AISoldier_Universal
{
    [Header("SOUND SYSTEM")]
    [SerializeField] LayerMask groundLayerMask;

    //==============================
    //AUDIO
    //==============================

    //Footsteps
    [SerializeField] private AudioClip[] FSNormDefault;
    [SerializeField] private AudioClip[] FSFastDefault;
    [SerializeField] private AudioClip[] FSSlowDefault;
    [SerializeField] private AudioClip[] FSDragDefault;
    [SerializeField] private AudioClip[] FSNormLino;
    [SerializeField] private AudioClip[] FSFastLino;
    [SerializeField] private AudioClip[] FSSlowLino;
    [SerializeField] private AudioClip[] FSDragLino;
    [SerializeField] private AudioClip[] FSNormMetalGrate;
    [SerializeField] private AudioClip[] FSFastMetalGrate;
    [SerializeField] private AudioClip[] FSSlowMetalGrate;
    [SerializeField] private AudioClip[] FSDragMetalGrate;
    [SerializeField] private GameObject FSPrefabMetalGrate;

    //Movement foley
    [SerializeField] private AudioClip[] ClothNorm;
    [SerializeField] private AudioClip[] ClothFast;
    [SerializeField] private AudioClip[] RubberNorm;
    [SerializeField] private AudioClip[] RubberFast;
    [SerializeField] private AudioClip[] GearNorm;
    [SerializeField] private AudioClip[] GearFast;
    [SerializeField] private AudioClip[] GearSweetener;
    [SerializeField] private AudioClip[] GunNorm;
    [SerializeField] private AudioClip[] GunFast;

    //Weapon sounds
    [SerializeField] private AudioClip[] GunFire;
    [SerializeField] private AudioClip[] GunMagOut;
    [SerializeField] private AudioClip[] GunMagIn;
    [SerializeField] private AudioClip[] GunBolt;

    enum Foley {ClothNorm, ClothFast, GearNorm, GearFast, GearSweetener, GunNorm, GunFast}
    enum GunSounds {GunFire, GunMagOut, GunMagIn, GunBolt}


    private void UpdateAudio()
    {
        
    }

    #region Event
    public void PlayFootstepSound(bool leftFoot)
        // ideally, called from an animation event
        // TESTED: can't be called in an animation event (for some reason) -- must use PlayFootstepSoundLeft() or ...Right()
        // Hypothesis: boolean parameter methods can't be called in animation events. Why????/?
    {
        SurfaceType material = GetSurfaceUnderfoot(leftFoot);
        AudioSource AS = leftFoot ? leftFS : rightFS;
        AudioClip[] clips = null;


        switch (material)
        {
            case SurfaceType.Default:
                clips = moveState == AIMoveState.Fast ? FSFastDefault :
                        moveState == AIMoveState.Slow ? FSSlowDefault :
                                                        FSNormDefault ;
                break;
            case SurfaceType.Linoleum:
                clips = moveState == AIMoveState.Fast ? FSFastLino :
                        moveState == AIMoveState.Slow ? FSSlowLino :
                                                        FSNormLino ;
                break;
            case SurfaceType.MetalGrate:
                clips = moveState == AIMoveState.Fast ? FSFastMetalGrate :
                        moveState == AIMoveState.Slow ? FSSlowMetalGrate :
                                                        FSNormMetalGrate ;
                CreateSelfDestructingSoundPlayer(FSPrefabMetalGrate, leftFoot);
                break;
        }

        PlayRandomSoundFromList(clips, AS);
        
        
    }
    public void PlayFootstepSoundLeft() { PlayFootstepSound(true); }
    public void PlayFootstepSoundRight() { PlayFootstepSound(false); }

    public void PlayFootstepDragSound(bool leftFoot)
    // ideally, called from an animation event
    // same logic as PlayFootstepSound()
    {
        SurfaceType material = GetSurfaceUnderfoot(leftFoot);
        AudioSource AS = leftFoot ? leftFS : rightFS;
        AudioClip[] clips = null;


        switch (material)
        {
            case SurfaceType.Default:
                clips = FSDragDefault;
                break;
            case SurfaceType.Linoleum:
                clips = FSDragLino;
                break;
            case SurfaceType.MetalGrate:
                clips = FSDragMetalGrate;
                CreateSelfDestructingSoundPlayer(FSPrefabMetalGrate, leftFoot);
                break;
        }

        PlayRandomSoundFromList(clips, AS, 0.5f);


    }
    public void PlayFootstepDragSoundLeft() { PlayFootstepDragSound(true); }
    public void PlayFootstepDragSoundRight() { PlayFootstepDragSound(false); }
    #endregion

    #region Playback
    public void PlayRandomSoundFromList(AudioClip[] audioClips, AudioSource audioSource, float volume)
    {
        if (audioClips == null || audioClips.Length == 0 || audioSource == null)
            return;

        if (volume <= 0f)
            return;

        int randomIndex = UnityEngine.Random.Range(0, audioClips.Length);
        audioSource.PlayOneShot(audioClips[randomIndex], volume);
    }

    public void PlayRandomSoundFromList(AudioClip[] audioClips, AudioSource audioSource)
    {
        PlayRandomSoundFromList(audioClips, audioSource, 1f);
    }
    #endregion

    #region Logic
    // A LOT OF THESE EXIST AS SEPARATE METHODS TO BE PLAYABLE THROUGH ANIMATION EVENTS
    private void PlayFoleySound(Foley foley, float volume = 1f)
    {
        AudioClip[] clips = foley switch
        {
            Foley.ClothNorm => ClothNorm,
            Foley.ClothFast => ClothFast,
            Foley.GearNorm => GearNorm,
            Foley.GearFast => GearFast,
            Foley.GearSweetener => GearSweetener,
            Foley.GunNorm => GunNorm,
            Foley.GunFast => GunFast,
            _ => ClothNorm
        };

        PlayRandomSoundFromList(clips, audioBody, volume);
    }
    private void PlayFoleySound(Foley foley)
    {
        PlayFoleySound(foley, 1f);
    }

    private void PlayGunSound(GunSounds sound, float volume = 1f)
    {
        AudioClip[] clips = sound switch
        {
            GunSounds.GunFire => GunFire,
            GunSounds.GunMagOut => GunMagOut,
            GunSounds.GunMagIn => GunMagIn,
            GunSounds.GunBolt => GunBolt,
            _ => GunFire
        };

        PlayRandomSoundFromList(clips, audioBody, volume);
    }
    private void PlayGunSound(GunSounds sound)
    {
        PlayGunSound(sound, 1f);
    }

    private SurfaceType GetSurfaceUnderfoot(bool leftFoot)
    {
        Transform foot = leftFoot ? leftFootIK.transform : rightFootIK.transform;
            Vector3 origin = foot.position + Vector3.up * 0.1f;
        float castDistance = 0.2f;

        bool hitSomething = Physics.Raycast(
            origin,
            Vector3.down,
            out RaycastHit hit,
            castDistance,
            groundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (hitSomething && hit.collider.TryGetComponent<SurfaceIdentity>(out var identity))
        {
            return identity.SurfaceType;
        }
        return SurfaceType.Default;

    }

    private void CreateSelfDestructingSoundPlayer(GameObject prefab, bool leftFoot)
    {
        Vector3 foot = leftFoot ? leftFootIK.transform.position : rightFootIK.transform.position;
        if (prefab != null)
        {
            Vector3 pos = foot + (Vector3.up * 0.25f);
            GameObject.Instantiate(prefab, pos, Quaternion.identity);
        }
    }
    #endregion
}
