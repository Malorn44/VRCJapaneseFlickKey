using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keyboard colour themes. The first is VRCJI's own look (the scene's colours as they are); the rest are
// polykey's theme packs, the same JSON files, from StreamingAssets/themes (credits in CREDITS.md there).
// Picked in the dashboard's テーマ row and remembered in PlayerPrefs. Themes colour the keys only: the
// keyboard's background stays transparent.
public class ThemeSystem : MonoBehaviour
{
    public static ThemeSystem Instance { get; private set; }

    [SerializeField] TextMeshProUGUI themeNameText; // the dashboard row's name label
    private const string ThemeKey = "Theme";
    private static readonly Color OriginalPicked = new Color32(0xB1, 0xDF, 0xFF, 0xFF); // as the flick petals
    private const string OriginalMark = "#00baf340";

    public class Theme
    {
        public string id;
        public string name;
        public bool isOriginal;
        public Color key, keyText, modifier, accent, accentText, legend, error, indicator, hover;

        public Color ColorFor(ThemeRole.Role role)
        {
            switch(role)
            {
                case ThemeRole.Role.Key: return key;
                case ThemeRole.Role.Modifier: return modifier;
                case ThemeRole.Role.Legend: return legend;
                case ThemeRole.Role.Accent: return accent;
                case ThemeRole.Role.AccentText: return accentText;
                case ThemeRole.Role.Error: return error;
                case ThemeRole.Role.Petal: return hover;
                default: return keyText;
            }
        }
    }

    private List<Theme> themes;

    // The project renders in linear colour space into a texture SteamVR shows as sRGB, so an Image's colour
    // reaches the headset darker than it was set (TextMeshPro's colours don't, they skip that conversion).
    // A theme's fill colours are set pre-brightened so the headset shows exactly the theme's value.
    public static Color ForImage(Color color) => QualitySettings.activeColorSpace == ColorSpace.Linear ? color.gamma : color;

    // Raised after a theme change, for what the keyboard colours itself (candidates, the text box).
    public event System.Action ThemeChanged;
    private int current;

    public Theme Current => themes[current];

    void Awake()
    {
        Instance = this;
        themes = LoadThemes(Path.Combine(Application.streamingAssetsPath, "themes"));
        var saved = PlayerPrefs.GetString(ThemeKey, themes[0].id);
        current = Mathf.Max(0, themes.FindIndex(t => t.id == saved));
    }

    void Start()
    {
        Apply();
    }

    // VRCJI's own look first, then the rest by name.
    public static List<Theme> LoadThemes(string directory)
    {
        var loaded = new List<Theme>();
        if(Directory.Exists(directory))
        {
            foreach(var path in Directory.GetFiles(directory, "*.json"))
            {
                try
                {
                    var theme = ParseTheme(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path));
                    if(theme != null) loaded.Add(theme);
                }
                catch(System.Exception e)
                {
                    Debug.LogWarning($"Theme {Path.GetFileName(path)} skipped: {e.Message}");
                }
            }
        }
        var themes = new List<Theme> { new Theme { id = "vrcji", name = "VRCJI", isOriginal = true } };
        themes.AddRange(loaded.OrderBy(t => t.name));
        return themes;
    }

    // polykey's theme pack: five required colours, the rest falling back as polykey's theme.ts does.
    private static Theme ParseTheme(string id, string text)
    {
        var json = JObject.Parse(text);
        if((string)json["kind"] != "theme") return null;
        var colors = (JObject)json["colors"];
        Color Get(string name, Color fallback) =>
            colors[name] != null && ColorUtility.TryParseHtmlString((string)colors[name], out var color) ? color : fallback;
        var surface = Get("surface", Color.black);
        var theme = new Theme
        {
            id = id,
            name = (string)json["name"] ?? id,
            key = Get("key", Color.white),
            keyText = Get("keyText", Color.black),
            modifier = Get("modifier", Color.gray),
        };
        theme.accent = Get("accent", theme.keyText);
        theme.accentText = Get("accentText", surface);
        theme.legend = Get("legend", theme.keyText);
        theme.error = Get("error", Get("fnLegend", theme.accent));
        theme.indicator = Get("indicator", theme.accent);
        theme.hover = Get("hover", theme.modifier);
        return theme;
    }

    public void NextTheme()
    {
        SetTheme((current + 1) % themes.Count);
    }

    public void PreviousTheme()
    {
        SetTheme((current - 1 + themes.Count) % themes.Count);
    }

    // After the settings are reset (PlayerPrefs cleared): back to VRCJI's own look.
    public void ResetTheme()
    {
        SetTheme(0);
    }

    private void SetTheme(int index)
    {
        current = index;
        PlayerPrefs.SetString(ThemeKey, Current.id);
        Apply();
    }

    public void Apply()
    {
        ApplyTo(Current);
        if(themeNameText != null) themeNameText.text = Current.name;
        ThemeChanged?.Invoke();
    }

    // Colours every ThemeRole in the scene (also used by the editor to preview themes).
    public static void ApplyTo(Theme theme)
    {
        foreach(var role in FindObjectsOfType<ThemeRole>(true)) role.Apply(theme);
    }

    // A candidate button in the bar: key colours, or accent for the picked one. VRCJI's own look is the
    // prefab's colours (originalFill, originalText), with the petal blue for the picked one.
    public void ColorCandidate(GameObject button, bool isPicked, Color originalFill, Color originalText)
    {
        var image = button.GetComponent<Image>();
        var label = button.GetComponentInChildren<TMP_Text>();
        if(Current.isOriginal)
        {
            image.color = isPicked ? OriginalPicked : originalFill;
            label.color = originalText;
            return;
        }
        image.color = ForImage(isPicked ? Current.accent : Current.key);
        label.color = isPicked ? Current.accentText : Current.keyText;
    }

    // A flick petal: hover, or accent for the one about to be typed.
    public void ColorPetal(GameObject petal, bool isPicked)
    {
        if(Current.isOriginal) return;
        petal.GetComponent<Image>().color = ForImage(isPicked ? Current.accent : Current.hover);
        var label = petal.GetComponentInChildren<TMP_Text>(true);
        if(label != null) label.color = isPicked ? Current.accentText : Current.keyText;
    }

    // The key being flicked: accent while the flick is at its centre (releasing types its own kana), as a
    // petal is while flicked to; its own colours otherwise. VRCJI's own look uses its petal blue.
    public void ColorMainKey(GameObject key, bool isPicked)
    {
        if(!isPicked)
        {
            foreach(var role in key.GetComponentsInChildren<ThemeRole>(true)) role.Apply(Current);
            return;
        }
        key.GetComponent<Image>().color = Current.isOriginal ? OriginalPicked : ForImage(Current.accent);
        if(Current.isOriginal) return;
        foreach(var label in key.GetComponentsInChildren<TMP_Text>(true)) label.color = Current.accentText;
    }

    // The key or candidate under the pointer (polykey's hover). VRCJI's own look: a light grey.
    public Color HoverColor => ForImage(Current.isOriginal ? new Color32(0xD8, 0xDC, 0xE2, 0xFF) : Current.hover);

    // The focused group in the text box: an accent tint (TMP draws <mark> over the text, so it can't be
    // polykey's solid accent). The other converted groups are in the legend colour.
    public string MarkColor => Current.isOriginal ? OriginalMark : "#" + ColorUtility.ToHtmlStringRGB(Current.accent) + "55";
    public string OtherGroupColor => Current.isOriginal ? null : "#" + ColorUtility.ToHtmlStringRGB(Current.legend);
}
