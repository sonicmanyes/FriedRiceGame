using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PanCookingAudio : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float idleSizzleVolume = 0.10f;
    [SerializeField, Range(0f, 1f)] private float boostedSizzleVolume = 0.34f;
    [SerializeField, Range(0f, 1f)] private float tossAccentVolume = 0.72f;

    [Header("Toss accent")]
    [SerializeField, Min(0.05f)] private float boostDuration = 0.42f;

    private PanTossController panController;
    private AudioSource loopSource;
    private AudioSource accentSource;
    private AudioClip sizzleClip;
    private AudioClip tossClip;
    private Coroutine boostRoutine;

    private void Awake()
    {
        panController = GetComponent<PanTossController>();
        CreateAudioSources();
        sizzleClip = CreateSizzleClip();
        tossClip = CreateTossClip();

        loopSource.clip = sizzleClip;
        loopSource.loop = true;
        loopSource.volume = idleSizzleVolume;
        loopSource.Play();
    }

    private void OnEnable()
    {
        if (panController != null)
            panController.TossStarted += PlayTossSound;
    }

    private void OnDisable()
    {
        if (panController != null)
            panController.TossStarted -= PlayTossSound;
    }

    private void OnDestroy()
    {
        if (sizzleClip != null) Destroy(sizzleClip);
        if (tossClip != null) Destroy(tossClip);
    }

    private void PlayTossSound()
    {
        accentSource.PlayOneShot(tossClip, tossAccentVolume);
        if (boostRoutine != null)
            StopCoroutine(boostRoutine);
        boostRoutine = StartCoroutine(BoostSizzle());
    }

    private IEnumerator BoostSizzle()
    {
        float elapsed = 0f;
        while (elapsed < boostDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / boostDuration);
            float envelope = Mathf.Sin(t * Mathf.PI);
            loopSource.volume = Mathf.Lerp(idleSizzleVolume, boostedSizzleVolume, envelope);
            yield return null;
        }
        loopSource.volume = idleSizzleVolume;
        boostRoutine = null;
    }

    private void CreateAudioSources()
    {
        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.spatialBlend = 0f;
        loopSource.dopplerLevel = 0f;

        accentSource = gameObject.AddComponent<AudioSource>();
        accentSource.playOnAwake = false;
        accentSource.spatialBlend = 0f;
        accentSource.dopplerLevel = 0f;
    }

    private static AudioClip CreateSizzleClip()
    {
        const int sampleRate = 44100;
        const int seconds = 3;
        float[] samples = new float[sampleRate * seconds];
        var random = new System.Random(7319);
        float previousNoise = 0f;
        float filtered = 0f;
        float crackle = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered = Mathf.Lerp(filtered, noise, 0.18f);
            float highFrequency = noise - previousNoise;
            previousNoise = noise;

            if (random.NextDouble() < 0.00075)
                crackle = (float)random.NextDouble() * 0.65f;
            crackle *= 0.965f;

            samples[i] = Mathf.Clamp(highFrequency * 0.085f + filtered * 0.055f + crackle, -0.8f, 0.8f);
        }

        AudioClip clip = AudioClip.Create("Procedural Gentle Sizzle", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateTossClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.72f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        var random = new System.Random(20260720);
        float previousNoise = 0f;
        float body = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float normalized = t / duration;
            float attack = Mathf.Clamp01(t / 0.018f);
            float decay = Mathf.Exp(-normalized * 5.2f);
            float envelope = attack * decay;

            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float sharp = noise - previousNoise;
            previousNoise = noise;
            body = Mathf.Lerp(body, noise, 0.045f);

            float metallic = Mathf.Sin(2f * Mathf.PI * (980f - 540f * normalized) * t) * 0.11f;
            samples[i] = Mathf.Clamp((sharp * 0.30f + body * 0.34f + metallic) * envelope, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create("Procedural Pan Toss JAH", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
