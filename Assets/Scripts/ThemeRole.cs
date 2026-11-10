using UnityEngine;
using UnityEngine.UI;

// What a keyboard Image or text is, for the theme to colour it (polykey's theme tokens).
public class ThemeRole : MonoBehaviour
{
    public enum Role
    {
        Key,        // kana keys, the text box
        Modifier,   // the side columns and the toolbar
        Label,      // key labels, the typed text, the pointer cursor (keyText: it reads on key and modifier)
        Legend,     // the text box's placeholder
        Accent,     // flick petals
        AccentText, // their labels
        Error,      // the hide key's ×
        Petal,      // flick petals: hover (the one about to be typed turns accent while flicking)
    }

    public Role role;
    private Color original;
    private bool isCaptured;

    // The VRCJI theme is the scene's own colours, kept from before any theme was applied.
    public void Apply(ThemeSystem.Theme theme)
    {
        var graphic = GetComponent<Graphic>();
        if(graphic == null) return;
        if(!isCaptured)
        {
            original = graphic.color;
            isCaptured = true;
        }
        if(theme.isOriginal) graphic.color = original;
        else graphic.color = graphic is Image ? ThemeSystem.ForImage(theme.ColorFor(role)) : theme.ColorFor(role);
    }
}
