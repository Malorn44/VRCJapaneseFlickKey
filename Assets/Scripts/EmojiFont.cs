using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Emoji for the keyboard: Kiwi Maru has none, so Noto Emoji (black and white, as VRChat's chatbox
// shows them; Resources/Fonts/NotoEmoji.ttf) is added to its fallbacks when the app starts.
public static class EmojiFont
{
    private const string Name = "Noto Emoji";

    public static void AddFallback(TMP_FontAsset font)
    {
        if(font == null) return;
        if(font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        var fallbacks = font.fallbackFontAssetTable;
        fallbacks.RemoveAll(f => f == null); // left from an earlier run in the editor
        if(fallbacks.Exists(f => f.name == Name)) return;
        var source = Resources.Load<Font>("Fonts/NotoEmoji");
        if(source == null)
        {
            Debug.LogWarning("Noto Emoji is missing: emoji candidates stay hidden");
            return;
        }
        var emoji = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        emoji.name = Name;
        fallbacks.Add(emoji);
    }

    // Whether the font (or its fallbacks) can draw every character of `text`. TextMeshPro draws one
    // character at a time, so emoji built from several (skin tones, flags, keycaps, 👨‍👩‍👧) would come
    // apart: those count as not drawable.
    public static bool CanDraw(TMP_FontAsset font, string text)
    {
        for(var i = 0; i < text.Length; i++)
        {
            uint c = text[i];
            if(char.IsHighSurrogate(text, i) && i + 1 < text.Length) c = (uint)char.ConvertToUtf32(text[i], text[++i]);
            if(IsJoining(c)) return false;
            if(TMP_FontAssetUtilities.GetCharacterFromFontAsset(c, font, true, FontStyles.Normal, FontWeight.Regular, out _) == null)
                return false;
        }
        return true;
    }

    private static bool IsJoining(uint c) =>
        c == 0x200D                        // zero-width joiner
        || c == 0x20E3                     // keycap
        || (c >= 0x1F3FB && c <= 0x1F3FF)  // skin tones
        || (c >= 0x1F1E6 && c <= 0x1F1FF)  // flag letters
        || (c >= 0xE0020 && c <= 0xE007F); // subdivision flag tags
}
