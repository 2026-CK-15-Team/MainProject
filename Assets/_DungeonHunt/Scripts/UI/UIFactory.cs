using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// uGUI를 코드로 조립하는 헬퍼. 아트가 들어오기 전 기본 모양 UI용 (08 §3.1 공용 시각 틀 5종의 자리 잡기).
// 메뉴 조작은 마우스만 (키보드 내비게이션 끔, 08 §3).
public static class UIFactory
{
    public static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.11f, 0.94f);
    public static readonly Color PanelLight = new Color(0.16f, 0.16f, 0.2f, 0.95f);
    public static readonly Color ButtonColor = new Color(0.24f, 0.26f, 0.34f, 1f);
    public static readonly Color ButtonHover = new Color(0.34f, 0.37f, 0.48f, 1f);
    public static readonly Color DisabledColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
    public static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f);
    public static readonly Color AccentColor = new Color(1f, 0.82f, 0.3f);

    public static Font Font => GameServices.Config != null && GameServices.Config.UIFont != null ? GameServices.Config.UIFont : PlaceholderArt.UIFont;

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 앵커/피벗 지정 배치. anchor는 (0,0)=왼쪽 아래 ~ (1,1)=오른쪽 위
    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    public static Image Panel(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = PlaceholderArt.Square;
        image.color = color;
        return image;
    }

    public static Text Label(string name, Transform parent, string text, int size, TextAnchor alignment, Color? color = null)
    {
        RectTransform rect = CreateRect(name, parent);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = Font;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color ?? TextColor;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.supportRichText = true;
        return label;
    }

    public static Button Button(string name, Transform parent, string text, Action onClick, int fontSize = 30)
    {
        Image image = Panel(name, parent, ButtonColor);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.4f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (onClick != null) button.onClick.AddListener(new UnityAction(onClick));
        Text label = Label("Text", image.transform, text, fontSize, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform, 6f);
        return button;
    }

    public static void SetButtonText(Button button, string text)
    {
        Text label = button.GetComponentInChildren<Text>();
        if (label != null) label.text = text;
    }

    public static Slider Slider(string name, Transform parent, float min, float max, float value, Action<float> onChanged)
    {
        RectTransform root = CreateRect(name, parent);
        Image back = Panel("Background", root, new Color(0.2f, 0.2f, 0.25f));
        Stretch(back.rectTransform);
        RectTransform fillArea = CreateRect("FillArea", root);
        Stretch(fillArea, 4f);
        Image fill = Panel("Fill", fillArea, AccentColor);
        Stretch(fill.rectTransform);
        RectTransform handleArea = CreateRect("HandleArea", root);
        Stretch(handleArea, 4f);
        Image handle = Panel("Handle", handleArea, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(16f, 0f);

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.SetValueWithoutNotify(value);
        if (onChanged != null) slider.onValueChanged.AddListener(new UnityAction<float>(onChanged));
        return slider;
    }

    public static RawImage Raw(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        var raw = rect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    // 아티팩트 카드 (아이콘, 등급 색, 이름, 세트, 효과) — 거래 및 선택 카드 팝업 공용 부품
    public static RectTransform ArtifactCard(string name, Transform parent, ArtifactDefinition def, Vector2 size, bool showDetails, string extraLine = null)
    {
        Image panel = Panel(name, parent, PanelLight);
        RectTransform rect = panel.rectTransform;
        rect.sizeDelta = size;
        if (def == null) return rect;

        Color gradeColor = ArtifactGradeUtility.Color(def.Grade);
        Image border = Panel("GradeBar", rect, gradeColor);
        Place(border.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size.x, 8f));

        Image icon = Panel("Icon", rect, def.Icon != null ? Color.white : gradeColor);
        if (def.Icon != null) icon.sprite = def.Icon;
        Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(96f, 96f));
        if (def.Icon == null) icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);

        string title = showDetails ? def.DisplayName : "???";
        Text nameText = Label("Name", rect, title, 30, TextAnchor.UpperCenter, gradeColor);
        Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(size.x - 20f, 40f));

        if (showDetails)
        {
            string setLine = def.Set != null ? $"{def.Set.DisplayName} 세트" : "범용";
            Text info = Label("Info", rect, $"{ArtifactGradeUtility.Display(def.Grade)} · {setLine}", 22, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.85f));
            Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -182f), new Vector2(size.x - 20f, 30f));
            Text effect = Label("Effect", rect, def.EffectText, 22, TextAnchor.UpperCenter);
            Place(effect.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(size.x - 30f, size.y - 260f));
        }
        if (!string.IsNullOrEmpty(extraLine))
        {
            Text extra = Label("Extra", rect, extraLine, 22, TextAnchor.LowerCenter, AccentColor);
            Place(extra.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(size.x - 20f, 60f));
        }
        return rect;
    }

    public static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }
}
