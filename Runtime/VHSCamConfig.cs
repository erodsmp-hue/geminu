using UnityEngine;

namespace VHSBackroomsCam
{
    [CreateAssetMenu(menuName = "VHS Backrooms Cam/Config", fileName = "VHSCamConfig")]
    public class VHSCamConfig : ScriptableObject
    {
        [Header("Canvas")]
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        public RenderMode renderMode = RenderMode.ScreenSpaceOverlay;
        public int sortingOrder = 500;

        [Header("Modules")]
        public bool createStaticNoiseOverlay = true;
        public bool createRecIndicator = true;
        public bool createTimestamp = true;
        public bool createTapeLabel = true;
        public bool createCrosshair = true;
        public bool createCornerGuides = true;
        public bool createTrackingLines = false;
        public bool createTopReadout = true;
        public bool createBlinkingRecText = true;

        [Header("Text")]
        public string tapeLabel = "CAM 01";
        public string timestampFormat = "yyyy-MM-dd  HH:mm:ss";
        public string recText = "REC";
        public string topReadout = "SP   AUTO   F2.8";

        [Header("Style")]
        public Color frameColor = new Color(0.82f, 0.86f, 0.8f, 0.62f);
        public Color softColor = new Color(0.72f, 0.78f, 0.72f, 0.34f);
        public Color warningColor = new Color(0.96f, 0.18f, 0.18f, 0.88f);
        public Color shadowColor = new Color(0f, 0f, 0f, 0.14f);
        public Color noiseTint = new Color(1f, 1f, 1f, 0.018f);
        public float borderThickness = 2f;
        public float cornerLength = 24f;
        public float cornerThickness = 2f;
        public float trackingLineThickness = 1f;
        public Vector2 safePadding = new Vector2(34f, 26f);
        public Vector2 crosshairSize = new Vector2(22f, 22f);
        public int fontSize = 22;
        public int smallFontSize = 16;
        public int tinyFontSize = 14;

        [Header("Runtime")]
        public bool addRuntimeDriver = true;
        public bool animateNoise = true;
        public bool animateRecBlink = true;
        public bool pulseTrackingLineAlpha = false;
        public float noiseSpeed = 0.022f;
        public float recBlinkInterval = 0.72f;
        public float trackingPulseSpeed = 1.1f;
        public bool useUnscaledTime = true;
    }
}
