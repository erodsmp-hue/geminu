using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VHSBackroomsCam
{
    public class VHSCamRuntime : MonoBehaviour
    {
        [SerializeField] private VHSCamConfig config;
        [SerializeField] private RawImage noiseImage;
        [SerializeField] private TMP_Text recText;
        [SerializeField] private TMP_Text timestampText;
        [SerializeField] private Graphic topTrackingLine;
        [SerializeField] private Graphic bottomTrackingLine;

        private float blinkTimer;
        private bool recVisible = true;
        private Color topBase;
        private Color bottomBase;

        public void SetConfig(VHSCamConfig newConfig)
        {
            config = newConfig;
        }

        private void Awake()
        {
            if (topTrackingLine != null) topBase = topTrackingLine.color;
            if (bottomTrackingLine != null) bottomBase = bottomTrackingLine.color;
        }

        private void Update()
        {
            if (config == null)
                return;

            float dt = config.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float timeNow = config.useUnscaledTime ? Time.unscaledTime : Time.time;

            if (timestampText != null)
                timestampText.text = System.DateTime.Now.ToString(config.timestampFormat);

            if (config.animateRecBlink && recText != null)
            {
                blinkTimer += dt;
                if (blinkTimer >= config.recBlinkInterval)
                {
                    blinkTimer = 0f;
                    recVisible = !recVisible;
                    recText.enabled = recVisible;
                }
            }

            if (config.animateNoise && noiseImage != null)
            {
                Rect uv = noiseImage.uvRect;
                uv.x = Mathf.Repeat(timeNow * config.noiseSpeed, 1f);
                uv.y = Mathf.Repeat(timeNow * (config.noiseSpeed * 0.19f), 1f);
                noiseImage.uvRect = uv;
            }

            if (config.pulseTrackingLineAlpha)
            {
                float wave = 0.09f + Mathf.PingPong(timeNow * config.trackingPulseSpeed, 0.08f);
                if (topTrackingLine != null)
                    topTrackingLine.color = new Color(topBase.r, topBase.g, topBase.b, wave);
                if (bottomTrackingLine != null)
                    bottomTrackingLine.color = new Color(bottomBase.r, bottomBase.g, bottomBase.b, wave * 0.85f);
            }
        }
    }
}
