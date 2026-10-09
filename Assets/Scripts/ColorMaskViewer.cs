
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class ColorMaskViewer : MonoBehaviour
{
    [Header("Images")]
    public RawImage sourceImage;
    public RawImage redMaskImage;
    public RawImage blueMaskImage;

    [Header("ROI 1 - Yellow (top-left origin)")]
    public int roiX = 400;
    public int roiY = 200;
    public int roiWidth = 800;
    public int roiHeight = 500;

    [Header("ROI 2 - Green (top-left origin)")]
    public int roi2X = 1000;
    public int roi2Y = 200;
    public int roi2Width = 400;
    public int roi2Height = 500;

    [Header("Red RGB range (0-255)")]
    [Range(0, 255)] public int redRMin = 150;
    [Range(0, 255)] public int redRMax = 255;
    [Range(0, 255)] public int redGMin = 0;
    [Range(0, 255)] public int redGMax = 100;
    [Range(0, 255)] public int redBMin = 0;
    [Range(0, 255)] public int redBMax = 100;

    [Header("Blue RGB range (0-255)")]
    [Range(0, 255)] public int blueRMin = 0;
    [Range(0, 255)] public int blueRMax = 100;
    [Range(0, 255)] public int blueGMin = 0;
    [Range(0, 255)] public int blueGMax = 180;
    [Range(0, 255)] public int blueBMin = 100;
    [Range(0, 255)] public int blueBMax = 255;

    [Header("Display")]
    public bool showROI1 = true;
    public bool showROI2 = true;

    Texture2D sourceTexture;
    Texture2D redTexture;
    Texture2D blueTexture;

    RectTransform frame1;
    RectTransform frame2;
    Image[] edges1;
    Image[] edges2;

    Texture oldSource;
    Rect oldUV;

    int oldX, oldY, oldW, oldH;
    int oldX2, oldY2, oldW2, oldH2;

    int oldRedRMin, oldRedRMax;
    int oldRedGMin, oldRedGMax;
    int oldRedBMin, oldRedBMax;

    int oldBlueRMin, oldBlueRMax;
    int oldBlueGMin, oldBlueGMax;
    int oldBlueBMin, oldBlueBMax;

    bool oldShowROI1, oldShowROI2;
    bool hasPreviousSettings;

    void Start()
    {
        CreateFrame(sourceImage, "ROI 1 Frame",
            Color.yellow, out frame1, out edges1);

        CreateFrame(sourceImage, "ROI 2 Frame",
            Color.green, out frame2, out edges2);

        Refresh();
    }

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            Refresh();
        }

        if (sourceImage == null)
            return;

        bool changed =
            !hasPreviousSettings ||
            sourceImage.texture != oldSource ||
            sourceImage.uvRect != oldUV ||
            roiX != oldX || roiY != oldY ||
            roiWidth != oldW || roiHeight != oldH ||
            roi2X != oldX2 || roi2Y != oldY2 ||
            roi2Width != oldW2 || roi2Height != oldH2 ||
            redRMin != oldRedRMin || redRMax != oldRedRMax ||
            redGMin != oldRedGMin || redGMax != oldRedGMax ||
            redBMin != oldRedBMin || redBMax != oldRedBMax ||
            blueRMin != oldBlueRMin || blueRMax != oldBlueRMax ||
            blueGMin != oldBlueGMin || blueGMax != oldBlueGMax ||
            blueBMin != oldBlueBMin || blueBMax != oldBlueBMax ||
            showROI1 != oldShowROI1 ||
            showROI2 != oldShowROI2;

        if (changed)
            Refresh();
    }

    void CreateFrame(
        RawImage target,
        string frameName,
        Color color,
        out RectTransform frame,
        out Image[] edges)
    {
        frame = null;
        edges = null;

        if (target == null)
            return;

        Transform existing = target.transform.Find(frameName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject go = new GameObject(
            frameName, typeof(RectTransform));

        frame = go.GetComponent<RectTransform>();
        frame.SetParent(target.transform, false);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = Vector2.zero;
        frame.offsetMax = Vector2.zero;

        edges = new Image[4];

        for (int i = 0; i < 4; i++)
        {
            GameObject edge = new GameObject(
                "Edge" + i,
                typeof(RectTransform),
                typeof(Image));

            edge.transform.SetParent(frame, false);

            edges[i] = edge.GetComponent<Image>();
            edges[i].color = color;
            edges[i].raycastTarget = false;
        }
    }

    void DrawFrame(
        RectTransform frame,
        Image[] edges,
        int x, int y, int width, int height,
        bool visible)
    {
        if (frame == null || edges == null ||
            sourceTexture == null)
            return;

        frame.gameObject.SetActive(visible);

        float displayW = frame.rect.width;
        float displayH = frame.rect.height;

        if (displayW <= 0 || displayH <= 0)
            return;

        float sx = displayW / sourceTexture.width;
        float sy = displayH / sourceTexture.height;

        int x0 = Mathf.Clamp(x, 0, sourceTexture.width);
        int y0 = Mathf.Clamp(y, 0, sourceTexture.height);
        int x1 = Mathf.Clamp(x + width, x0, sourceTexture.width);
        int y1 = Mathf.Clamp(y + height, y0, sourceTexture.height);

        float left = x0 * sx;
        float right = x1 * sx;
        float top = displayH - y0 * sy;
        float bottom = displayH - y1 * sy;

        float thickness = 3f;

        SetEdge(edges[0], left, top - thickness,
            right - left, thickness);

        SetEdge(edges[1], left, bottom,
            right - left, thickness);

        SetEdge(edges[2], left, bottom,
            thickness, top - bottom);

        SetEdge(edges[3], right - thickness, bottom,
            thickness, top - bottom);
    }

    void SetEdge(
        Image edge, float x, float y,
        float width, float height)
    {
        RectTransform rt = edge.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    bool InsideROI(int x, int y, int w, int h, int px, int py)
    {
        return px >= x && px < x + w &&
               py >= y && py < y + h;
    }

    public void Refresh()
    {
        if (sourceImage == null ||
            redMaskImage == null ||
            blueMaskImage == null)
            return;

        Texture input = sourceImage.texture;

        if (input == null)
        {
            Debug.LogWarning("Source Image has no texture.");
            return;
        }

        int w = input.width;
        int h = input.height;

        if (input is Texture2D tex2D)
        {
            sourceTexture = tex2D;
        }
        else if (input is RenderTexture rt)
        {
            if (sourceTexture == null ||
                sourceTexture.width != w ||
                sourceTexture.height != h)
            {
                if (sourceTexture != null)
                    Destroy(sourceTexture);

                sourceTexture = new Texture2D(
                    w, h, TextureFormat.RGBA32, false);
            }

            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = rt;
                sourceTexture.ReadPixels(
                    new Rect(0, 0, w, h), 0, 0, false);
                sourceTexture.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }
        else
        {
            Debug.LogWarning("Unsupported texture type.");
            return;
        }

        if (redTexture == null ||
            redTexture.width != w ||
            redTexture.height != h)
        {
            if (redTexture != null)
                Destroy(redTexture);

            if (blueTexture != null)
                Destroy(blueTexture);

            redTexture = new Texture2D(
                w, h, TextureFormat.RGBA32, false);

            blueTexture = new Texture2D(
                w, h, TextureFormat.RGBA32, false);
        }

        Color32[] pixels = sourceTexture.GetPixels32();
        Color32[] red = new Color32[pixels.Length];
        Color32[] blue = new Color32[pixels.Length];

        Color32 black = new Color32(0, 0, 0, 255);
        Color32 white = new Color32(255, 255, 255, 255);

        for (int i = 0; i < pixels.Length; i++)
        {
            red[i] = black;
            blue[i] = black;
        }

        int x0 = Mathf.Clamp(
            Mathf.Min(roiX, roi2X), 0, w);

        int y0 = Mathf.Clamp(
            Mathf.Min(roiY, roi2Y), 0, h);

        int x1 = Mathf.Clamp(
            Mathf.Max(roiX + roiWidth, roi2X + roi2Width), 0, w);

        int y1 = Mathf.Clamp(
            Mathf.Max(roiY + roiHeight, roi2Y + roi2Height), 0, h);

        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                bool inROI1 = InsideROI(
                    roiX, roiY, roiWidth, roiHeight, x, y);

                bool inROI2 = InsideROI(
                    roi2X, roi2Y, roi2Width, roi2Height, x, y);

                if (!inROI1 && !inROI2)
                    continue;

                int textureY = h - 1 - y;
                int i = textureY * w + x;

                Color32 c = pixels[i];

                bool isRed =
                    c.r >= redRMin && c.r <= redRMax &&
                    c.g >= redGMin && c.g <= redGMax &&
                    c.b >= redBMin && c.b <= redBMax;

                bool isBlue =
                    c.r >= blueRMin && c.r <= blueRMax &&
                    c.g >= blueGMin && c.g <= blueGMax &&
                    c.b >= blueBMin && c.b <= blueBMax;

                if (isRed)
                    red[i] = white;

                if (isBlue)
                    blue[i] = white;
            }
        }

        redTexture.SetPixels32(red);
        redTexture.Apply(false, false);

        blueTexture.SetPixels32(blue);
        blueTexture.Apply(false, false);

        redMaskImage.texture = redTexture;
        blueMaskImage.texture = blueTexture;

        redMaskImage.uvRect = sourceImage.uvRect;
        blueMaskImage.uvRect = sourceImage.uvRect;

        oldSource = sourceImage.texture;
        oldUV = sourceImage.uvRect;

        oldX = roiX;
        oldY = roiY;
        oldW = roiWidth;
        oldH = roiHeight;

        oldX2 = roi2X;
        oldY2 = roi2Y;
        oldW2 = roi2Width;
        oldH2 = roi2Height;

        oldRedRMin = redRMin;
        oldRedRMax = redRMax;
        oldRedGMin = redGMin;
        oldRedGMax = redGMax;
        oldRedBMin = redBMin;
        oldRedBMax = redBMax;

        oldBlueRMin = blueRMin;
        oldBlueRMax = blueRMax;
        oldBlueGMin = blueGMin;
        oldBlueGMax = blueGMax;
        oldBlueBMin = blueBMin;
        oldBlueBMax = blueBMax;

        oldShowROI1 = showROI1;
        oldShowROI2 = showROI2;
        hasPreviousSettings = true;

        DrawFrame(frame1, edges1,
            roiX, roiY, roiWidth, roiHeight, showROI1);

        DrawFrame(frame2, edges2,
            roi2X, roi2Y, roi2Width, roi2Height, showROI2);
    }

    void OnDestroy()
    {
        if (redTexture != null)
            Destroy(redTexture);

        if (blueTexture != null)
            Destroy(blueTexture);

        // sourceTexture may reference the original Texture2D.
        // Do not destroy it here.
    }
}