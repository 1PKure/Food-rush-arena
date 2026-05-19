using UnityEngine;
using UnityEngine.UI;

public class AnimatedBackground : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image targetImage;

    [Header("Animation")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 8f;
    [SerializeField] private bool playOnStart = true;

    private int currentFrameIndex;
    private float timer;
    private bool isPlaying;

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }
    private void Start()
    {
        isPlaying = playOnStart;

        if (frames != null && frames.Length > 0 && targetImage != null)
        {
            targetImage.sprite = frames[0];
        }
    }

    private void Update()
    {
        if (!isPlaying || targetImage == null || frames == null || frames.Length == 0)
        {
            return;
        }

        timer += Time.deltaTime;

        float frameDuration = 1f / framesPerSecond;

        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrameIndex = (currentFrameIndex + 1) % frames.Length;
            targetImage.sprite = frames[currentFrameIndex];
        }
    }

    public void Play()
    {
        isPlaying = true;
    }

    public void Stop()
    {
        isPlaying = false;
    }

    public void SetFramesPerSecond(float newFramesPerSecond)
    {
        framesPerSecond = Mathf.Max(1f, newFramesPerSecond);
    }
}