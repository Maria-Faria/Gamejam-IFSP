using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
  [SerializeField] private AudioClip music;
  [Range(0f, 1f)]
  [SerializeField] private float volume = 0.35f;
  [Tooltip("Segundos para a música subir do silêncio até o volume final.")]
  [SerializeField] private float fadeInSeconds = 2f;
  [Tooltip("Segundos de transição no fim da faixa: o final some aos poucos enquanto o começo volta aos poucos, sem corte seco.")]
  [SerializeField] private float loopCrossfadeSeconds = 3f;

  private AudioSource[] sources;
  private int current;
  private float startFade;
  private float loopFade = 1f;

  public float FadeMultiplier { get; set; } = 1f;

  public void Start()
  {
    if (music == null)
    {
      return;
    }

    sources = new[] { CreateSource(), CreateSource() };
    sources[current].Play();
  }

  private AudioSource CreateSource()
  {
    AudioSource source = gameObject.AddComponent<AudioSource>();
    source.clip = music;
    source.loop = false;
    source.playOnAwake = false;
    source.spatialBlend = 0f;
    source.priority = 0;
    source.volume = 0f;
    return source;
  }

  public void Update()
  {
    if (sources == null)
    {
      return;
    }

    startFade = fadeInSeconds > 0f ? Mathf.MoveTowards(startFade, 1f, Time.unscaledDeltaTime / fadeInSeconds) : 1f;

    AudioSource playing = sources[current];
    AudioSource previous = sources[1 - current];
    float crossfade = Mathf.Min(loopCrossfadeSeconds, music.length * 0.25f);

    if (playing.isPlaying && playing.time >= music.length - crossfade)
    {
      current = 1 - current;
      previous = playing;
      playing = sources[current];
      playing.time = 0f;
      playing.Play();
      loopFade = 0f;
    }
    else if (!playing.isPlaying)
    {
      playing.time = 0f;
      playing.Play();
      loopFade = 1f;
    }

    loopFade = crossfade > 0f ? Mathf.MoveTowards(loopFade, 1f, Time.unscaledDeltaTime / crossfade) : 1f;

    float baseVolume = volume * startFade * FadeMultiplier;
    playing.volume = baseVolume * Mathf.Sin(loopFade * Mathf.PI * 0.5f);
    previous.volume = previous.isPlaying ? baseVolume * Mathf.Cos(loopFade * Mathf.PI * 0.5f) : 0f;
    if (loopFade >= 1f && previous.isPlaying)
    {
      previous.Stop();
    }
  }
}
