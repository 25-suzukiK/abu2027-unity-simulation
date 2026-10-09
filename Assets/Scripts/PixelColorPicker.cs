
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PixelColorPicker : MonoBehaviour
{
    public RawImage display;

    void Update()
    {
        // 左クリックしたとき
        if (Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (display == null || display.texture == null)
            return;

        // マウス位置をRawImage内の座標に変換
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            display.rectTransform,
            Mouse.current.position.ReadValue(),
            null,
            out Vector2 localPoint))
            return;

        Rect rect = display.rectTransform.rect;

        if (!rect.Contains(localPoint))
            return;

        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;

        Texture2D texture = display.texture as Texture2D;

        if (texture == null)
        {
            Debug.LogWarning("表示中の画像がTexture2Dではありません。");
            return;
        }

        int x = Mathf.Clamp(
            Mathf.FloorToInt(u * texture.width),
            0, texture.width - 1);

        int y = Mathf.Clamp(
            Mathf.FloorToInt(v * texture.height),
            0, texture.height - 1);

        Color color = texture.GetPixel(x, y);

        Debug.Log(
            $"Pixel ({x}, {y}) : " +
            $"R={Mathf.RoundToInt(color.r * 255)}, " +
            $"G={Mathf.RoundToInt(color.g * 255)}, " +
            $"B={Mathf.RoundToInt(color.b * 255)}");
    }
}