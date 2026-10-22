using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
  [SerializeField] private AudioClip music;
  [Range(0f, 1f)]
  [SerializeField] private float volume = 0.35f;
  [Tooltip("Segundos para a música subir do silêncio até o volume final.")]
  [SerializeField] private float fadeInSeconds = 2f;

  private AudioSource source;

  public void Start()
  {
    if (music == null)
    {
      return;
    }

    source = gameObject.AddComponent<AudioSource>();
    source.clip = music;
    source.loop = true;
    source.playOnAwake = false;
    source.spatialBlend = 0f;
    source.priority = 0;
    source.volume = fadeInSeconds > 0f ? 0f : volume;
    source.Play();
  }

  public void Update()
  {
    if (source != null && source.volume < volume)
    {
      source.volume = Mathf.MoveTowards(source.volume, volume, volume / Mathf.Max(0.01f, fadeInSeconds) * Time.deltaTime);
    }
  }
}
