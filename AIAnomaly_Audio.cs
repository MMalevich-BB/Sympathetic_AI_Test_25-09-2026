using System.Collections.Generic;
using UnityEngine;

public partial class AIAnomaly_Doobie : MonoBehaviour
{
    [SerializeField] float randomSoundIntervalMin = 9f; // maximum length of any random audio file so there's no interruption
    [SerializeField] float randomSoundIntervalMax = 24f;
    private float randomSoundTargetTime = 0f;
    private float randomSoundTimer = 0f;
    private bool isTimerRunning = false;

    [SerializeField] AudioClip[] audioRandomNormal;
    [SerializeField] AudioClip[] audioRandomHunt;
    [SerializeField] AudioClip[] audioAlert;
    [SerializeField] AudioClip[] audioDistracted;
    [SerializeField] AudioClip[] audioLoss;
    [SerializeField] AudioClip[] audioTeleportD;
    [SerializeField] AudioClip[] audioTeleportA;
    [SerializeField] AudioClip[] audioAttack;

    private void UpdateAudio()
    {
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (!isTimerRunning)
        {
            randomSoundTimer = 0f;
            isTimerRunning = true;
            randomSoundTargetTime = UnityEngine.Random.Range(randomSoundIntervalMin, randomSoundIntervalMax);
            return;
        }

        if (randomSoundTimer >= randomSoundTargetTime)
        {
            PlayRandomSound();
            isTimerRunning = false;
        }
        randomSoundTimer += Time.deltaTime;
    }

    private void PlayDistractedSound()
    {
        isTimerRunning = false;
        PlayRandomSoundFromList(audioDistracted, audioBody, true);
    }
    private void PlayLossSound()
    {
        isTimerRunning = false;
        PlayRandomSoundFromList(audioLoss, audioBody, true);
    }
    private void PlayAlertSound()
    {
        isTimerRunning = false;
        PlayRandomSoundFromList(audioAlert, audioBody, true);
    }
    private void PlayTeleportDisappearSound()
    {
        isTimerRunning = false;
        PlayRandomSoundFromList(audioTeleportD, audioBody, true);
    }
    private void PlayTeleportAppearSound()
    {
        isTimerRunning = false;
        PlayRandomSoundFromList(audioTeleportA, audioBody, true);
    }

    private void PlayRandomSound()
    {
        AudioClip[] clips;

        if (currentState == DoobieStates.Idle ||
            currentState == DoobieStates.Patrol ||
            currentState == DoobieStates.Distracted)
            clips = audioRandomNormal;
        else if (currentState == DoobieStates.Hunt)
            clips = audioRandomHunt;
        else return;

        PlayRandomSoundFromList(clips, audioBody, 1f, false);
    }
    public void PlayRandomSoundFromList(AudioClip[] audioClips, AudioSource audioSource, float volume, bool OneShot)
    {
        if (audioClips == null || audioClips.Length == 0 || audioSource == null)
            return;

        if (volume <= 0f)
            return;

        int randomIndex = UnityEngine.Random.Range(0, audioClips.Length);

        if (OneShot)
            audioSource.PlayOneShot(audioClips[randomIndex], volume);
        else
        {
            if (audioSource.isPlaying) audioSource.Stop();
            audioSource.clip = audioClips[randomIndex];
            audioSource.volume = volume;
            audioSource.Play();
        }
    }

    public void PlayRandomSoundFromList(AudioClip[] audioClips, AudioSource audioSource, bool OneShot)
    {
        PlayRandomSoundFromList(audioClips, audioSource, 1f, OneShot);
    }
}