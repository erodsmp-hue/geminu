using UnityEngine;
using UnityEngine.UI;
using System;

public class CamcorderOSD : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Text timecodeText;
    [SerializeField] private Text dateText;
    [SerializeField] private Text recText;
    [SerializeField] private RectTransform uiRoot;

    [Header("Settings")]
    [SerializeField] private bool useFake90sDate = true;
    [SerializeField] private int fakeYear = 1996;
    [SerializeField] private float blinkRate = 0.6f;
    [SerializeField] private bool enableTapeJitter = true;
    [SerializeField] private float jitterIntensity = 1.5f;
    [SerializeField] private float jitterFrequency = 0.1f;

    private float timer;
    private Vector2 originalRootPos;

    void Start()
    {
        if (uiRoot != null) originalRootPos = uiRoot.anchoredPosition;
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Timecode (counting up from start)
        if (timecodeText != null)
        {
            TimeSpan t = TimeSpan.FromSeconds(timer);
            timecodeText.text = string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
        }

        // Blinking REC icon
        if (recText != null)
        {
            recText.enabled = Mathf.FloorToInt(timer / blinkRate) % 2 == 0;
        }

        // Camcorder Date Format (e.g. AM 12:45 \n OCT. 31 1996)
        if (dateText != null)
        {
            DateTime now = DateTime.Now;
            if (useFake90sDate)
            {
                now = new DateTime(fakeYear, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            }
            dateText.text = now.ToString("tt hh:mm\nMMM. dd yyyy").ToUpper();
        }

        // VHS Tape Tracking Jitter
        if (enableTapeJitter && uiRoot != null)
        {
            if (UnityEngine.Random.value < jitterFrequency)
            {
                // Shift UI up or down slightly like a worn tape
                uiRoot.anchoredPosition = originalRootPos + new Vector2(0, UnityEngine.Random.Range(-jitterIntensity, jitterIntensity));
            }
            else
            {
                uiRoot.anchoredPosition = Vector2.Lerp(uiRoot.anchoredPosition, originalRootPos, Time.deltaTime * 10f);
            }
        }
    }
}
