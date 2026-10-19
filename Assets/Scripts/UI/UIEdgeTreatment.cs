using UnityEngine;
using UnityEngine.UI;

public static class UIEdgeTreatment
{
    private static Sprite _surface;

    // Share the authored nine-slice already used by the friend challenge sheet.
    public static void Surface(Image image, Color edge, bool control = false)
    {
        if (image == null) return;
        if (_surface == null) _surface = Resources.Load<Sprite>("UI/ChallengeRounded");
        if (_surface != null)
        {
            image.sprite = _surface;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.4f;
        }
        if (control && image.GetComponent<UIControlFace>() == null)
            image.gameObject.AddComponent<UIControlFace>();
        Outline outline = image.GetComponent<Outline>();
        if (outline == null) outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = edge;
        outline.effectDistance = new Vector2(1.6f, -1.6f);
        outline.useGraphicAlpha = true;
    }

    public static void ButtonStates(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.78f, .84f, .86f, 1f);
        colors.disabledColor = new Color(.60f, .65f, .67f, .66f);
        colors.fadeDuration = .09f;
        button.colors = colors;
    }

    public static void Selection(Button button, bool selected)
    {
        if (button == null) return;
        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
            outline.effectColor = selected
                ? EchoRunUITheme.PageSelectedEdge : EchoRunUITheme.PageRule;
        // Selection must stay visible independently of the button's pointer
        // highlight, including after another control receives keyboard focus.
        Transform existing = button.transform.Find("SelectionRule");
        if (existing == null && !selected) return;
        if (existing == null)
        {
            var rule = new GameObject("SelectionRule", typeof(Image));
            rule.transform.SetParent(button.transform, false);
            Image image = rule.GetComponent<Image>();
            image.color = EchoRunUITheme.PageEcho;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(.32f, 0f);
            rect.anchorMax = new Vector2(.68f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 3f);
            rect.anchoredPosition = new Vector2(0f, 7f);
            existing = rule.transform;
        }
        existing.gameObject.SetActive(selected);
    }
}
