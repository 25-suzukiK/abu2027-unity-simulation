
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class RoiDatasetCapture : MonoBehaviour
{
    [Header("Camera image")]
    [SerializeField] private RenderTexture sourceTexture;

    [Header("ROI (origin: top-left)")]
    [SerializeField] private int roiX = 100;
    [SerializeField] private int roiY = 100;
    [SerializeField] private int roiWidth = 300;
    [SerializeField] private int roiHeight = 300;

    [Header("Capture settings")]
    [SerializeField] private KeyCode captureKey = KeyCode.Q;
    [SerializeField] private string folderName = "Dataset/ROI";

    private string saveDirectory;
    private int imageIndex = 1;

    private void Start()
    {
        // Save inside the Unity project folder.
        saveDirectory = Path.Combine(
            Application.dataPath,
            "..",
            folderName
        );

        saveDirectory = Path.GetFullPath(saveDirectory);
        Directory.CreateDirectory(saveDirectory);

        // Continue numbering if images already exist.
        while (File.Exists(GetFilePath(imageIndex)))
        {
            imageIndex++;
        }

        Debug.Log($"ROI capture ready. Press {captureKey} to save.");
        Debug.Log($"Save directory: {saveDirectory}");
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.qKey.wasPressedThisFrame)
        {
            CaptureROI();
        }
    }

    private void CaptureROI()
    {
        if (sourceTexture == null)
        {
            Debug.LogError("Source Texture is not assigned.");
            return;
        }

        int width = sourceTexture.width;
        int height = sourceTexture.height;

        // Validate ROI bounds.
        if (roiX < 0 || roiY < 0 ||
            roiWidth <= 0 || roiHeight <= 0 ||
            roiX + roiWidth > width ||
            roiY + roiHeight > height)
        {
            Debug.LogError(
                $"ROI is outside the image. " +
                $"Image: {width}x{height}, " +
                $"ROI: X={roiX}, Y={roiY}, " +
                $"W={roiWidth}, H={roiHeight}"
            );
            return;
        }

        RenderTexture previous = RenderTexture.active;
        Texture2D image = null;

        try
        {
            RenderTexture.active = sourceTexture;

            image = new Texture2D(
                roiWidth,
                roiHeight,
                TextureFormat.RGB24,
                false
            );

            // Unity's pixel origin is bottom-left.
            int yBottom = height - roiY - roiHeight;

            image.ReadPixels(
                new Rect(roiX, yBottom, roiWidth, roiHeight),
                0,
                0
            );

            image.Apply();

            byte[] png = image.EncodeToPNG();
            string path = GetFilePath(imageIndex);

            File.WriteAllBytes(path, png);

            Debug.Log($"Saved ROI image: {path}");
            imageIndex++;
        }
        catch (Exception e)
        {
            Debug.LogError($"ROI capture failed: {e.Message}");
        }
        finally
        {
            RenderTexture.active = previous;

            if (image != null)
            {
                Destroy(image);
            }
        }
    }

    private string GetFilePath(int index)
    {
        return Path.Combine(
            saveDirectory,
            $"roi_{index:D4}.png"
        );
    }
}