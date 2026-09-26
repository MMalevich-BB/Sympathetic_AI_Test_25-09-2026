using UnityEngine;


public partial class AISoldier_Universal
{
    [Header("Speech/Voice")]
    [SerializeField] private AudioClip[] voBreath;
    [SerializeField] private AudioClip[] voRadioClickGeneric;
    [SerializeField] private float voBreathInterval = 4f;
    [SerializeField] private float voBreathIntervalDelta = 0.5f;
    [SerializeField] private float voRadioClickInterval = 8f;
    private float voBreathTimer = 0f;
    private float voRadioClickTimer = 0f;
    private bool voIsBreathQueued = false;
    private bool voIsClickQueued = false;
    private bool voIsSpeaking = false;
    private void UpdateSpeech()
    {
        HandleBreath();
        HandleRadioClicks();
    }

    #region Timed
    private void HandleBreath()
    {
        if (voIsSpeaking && audioSpeech.isPlaying)
        {
            audioSpeech.Stop();
            return;
            //we call audioSpeech.Stop() here because breathing sounds are not OneShots and are directly controllable from the AudioSource. Speech will be played as OneShots, which can't be controlled, and thus audioSpeech.Stop() won't stop any speech currently playing -- only breathing noises.
        }

        if (!voIsBreathQueued)
        {
            voBreathTimer = voBreathInterval +
                            UnityEngine.Random.Range(-voBreathIntervalDelta, voBreathIntervalDelta);
            voIsBreathQueued = true;
        }
        else if (voBreathTimer < 0f)
        {
            voIsBreathQueued = false;
            PlayRandomSoundFromListControllable(voBreath, audioSpeech, 0.4f);
        }
        else if (voIsBreathQueued) voBreathTimer -= Time.deltaTime;
    }

    private void HandleRadioClicks()
    {
        if (!voIsClickQueued)
        {
            voRadioClickTimer = voRadioClickInterval +
                            UnityEngine.Random.Range(-3f, 12f);
            voIsClickQueued = true;
        }
        else if (voRadioClickTimer < 0f)
        {
            voIsClickQueued = false;
            PlayRandomSoundFromList(voRadioClickGeneric, audioSpeech, 0.4f);
        }
        else if (voIsClickQueued) voRadioClickTimer -= Time.deltaTime;

        
    }
    #endregion

    #region Logic
    public void PlayRandomSoundFromListControllable(AudioClip[] audioClips, AudioSource audioSource, float volume)
    {
        if (audioClips == null || audioClips.Length == 0 || audioSource == null)
            return;

        if (volume <= 0f)
            return;

        int randomIndex = UnityEngine.Random.Range(0, audioClips.Length);
        AudioClip clip = audioClips[randomIndex];
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();
    }
    #endregion
}