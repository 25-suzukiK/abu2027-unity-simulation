using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CameraFreeze : MonoBehaviour
{
    public RawImage display;
    public RenderTexture cameraRT;

    private Texture2D frozenImage;
    private bool isFrozen = false;
    private bool isCapturing = false;

    void Start()
    {
        display.texture = cameraRT;
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.pKey.wasPressedThisFrame && !isCapturing)
        {
            if (isFrozen)
            {
                display.texture = cameraRT;
                isFrozen = false;
            }
            else
            {
                StartCoroutine(CaptureImage());
            }
        }
    }

    IEnumerator CaptureImage()
    {
        isCapturing = true;
        yield return new WaitForEndOfFrame();

        if (frozenImage != null)
            Destroy(frozenImage);

        frozenImage = new Texture2D(
            cameraRT.width,
            cameraRT.height,
            TextureFormat.RGBA32,
            false,
            false
        );

        RenderTexture previous = RenderTexture.active;

        RenderTexture temporary = RenderTexture.GetTemporary(
            cameraRT.width,
            cameraRT.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB
        );

        Graphics.Blit(cameraRT, temporary);

        RenderTexture.active = temporary;
        frozenImage.ReadPixels(
            new Rect(0, 0, temporary.width, temporary.height),
            0, 0
        );
        frozenImage.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);

        display.texture = frozenImage;
        isFrozen = true;
        isCapturing = false;
    }

    void OnDestroy()
    {
        if (frozenImage != null)
            Destroy(frozenImage);
    }
}