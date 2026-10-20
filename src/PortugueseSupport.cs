using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static partial class PortugueseSupport
{
    static TMP_FontAsset fallback;
    static bool initializing;
    sealed class LayoutState
    {
        public TMP_Text Text;
        public float Maximum;
        public bool Wrap;
    }
    static readonly Dictionary<int, LayoutState> layouts = new Dictionary<int, LayoutState>();

    public static UnityEngine.TextCore.LowLevel.FontEngineError LoadFace(Font font, int size)
    {
        if (font != null && font.name.IndexOf("Georgia", StringComparison.OrdinalIgnoreCase) >= 0)
            return UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "georgia.ttf"), size);
        return UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(font, size);
    }

    static void EnsureFont()
    {
        if (fallback != null) return;
        Font font = Font.CreateDynamicFontFromOSFont("Georgia", 90);
        fallback = TMP_FontAsset.CreateFontAsset(font, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        if (fallback == null) throw new Exception("Unable to create Portuguese fallback font.");
        fallback.name = "Portuguese Latin fallback";
        UnityEngine.Object.DontDestroyOnLoad(font);
        UnityEngine.Object.DontDestroyOnLoad(fallback);
        string missing;
        bool ok = fallback.TryAddCharacters("áàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇºª–—…", out missing, false);
        Debug.Log("PTBR v3 font ready; complete=" + ok + "; missing=" + missing);
        if (TMP_Settings.fallbackFontAssets != null && !TMP_Settings.fallbackFontAssets.Contains(fallback))
            TMP_Settings.fallbackFontAssets.Add(fallback);
    }

    // Called again immediately before rendering: the game may reset font settings after OnEnable.
    public static void Configure(TMP_Text text)
    {
        if (initializing || text == null) return;
        try
        {
            initializing = true;
            string localized = TranslateLabel(text.text);
            if (localized != text.text) text.text = localized;
            EnsureFont();
            TMP_FontAsset original = text.font;
            if (original != null)
            {
                if (original.fallbackFontAssetTable == null)
                    original.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (original != fallback && !original.fallbackFontAssetTable.Contains(fallback))
                    original.fallbackFontAssetTable.Add(fallback);
            }
            LayoutState state;
            int id = text.GetInstanceID();
            if (!layouts.TryGetValue(id, out state) || state.Text != text)
            {
                state = new LayoutState();
                state.Text = text;
                state.Maximum = text.enableAutoSizing && text.fontSizeMax > 0 ? text.fontSizeMax : text.fontSize;
                state.Wrap = text.enableWordWrapping;
                layouts[id] = state;
            }
            else if (!text.enableAutoSizing && text.fontSize > 0)
            {
                // Respect an explicit size assigned by the game, then fit that size to the box.
                state.Maximum = text.fontSize;
            }
            if (state.Maximum <= 0 || text.rectTransform.rect.width <= 0 || text.rectTransform.rect.height <= 0) return;
            // A percentage of fontSizeMax is unsafe: several game labels use a very
            // large maximum while their actual fitted size is less than 20 points.
            float minimum = Mathf.Min(5f, state.Maximum);
            if (!Mathf.Approximately(text.fontSizeMax, state.Maximum)) text.fontSizeMax = state.Maximum;
            if (!Mathf.Approximately(text.fontSizeMin, minimum)) text.fontSizeMin = minimum;
            if (!text.enableAutoSizing) text.enableAutoSizing = true;
            bool shortButton = text.GetComponentInParent<Button>() != null && text.text != null && text.text.Length <= 24;
            bool wrap = shortButton ? false : state.Wrap;
            if (text.enableWordWrapping != wrap) text.enableWordWrapping = wrap;
        }
        catch (Exception e) { Debug.LogError("PTBR v3 font setup: " + e); }
        finally { initializing = false; }
    }

    // Used only by the isolated preview; no game assembly in the delivered package calls this.
    public static void Probe()
    {
        var go = new GameObject("Portuguese layout verification");
        go.AddComponent<PortugueseLayoutProbe>();
        UnityEngine.Object.DontDestroyOnLoad(go);
    }
}

public sealed class PortugueseLayoutProbe : MonoBehaviour
{
    System.Collections.IEnumerator Start()
    {
        yield return null;
        var canvasObject = new GameObject("Portuguese layout test canvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 800);
        string[] labels = { "Jogar gemas", "Concluir", "Você ainda pode agir!", "Clique ou arraste uma gema ou relíquia para jogar.", "Perder 1 carga", "Sofrer 2 de dano", "<b>PERSISTENTE:</b> Qualquer jogador perde 1 carga ou sofre 2 de dano.", "Fragmento de Esmeralda\nGanhe 1 éter.\n<b>OU</b>\nQualquer jogador ganha 1 de vida." };
        Vector2[] sizes = { new Vector2(190,44), new Vector2(170,42),new Vector2(420,32),new Vector2(700,38),new Vector2(190,72),new Vector2(190,72),new Vector2(210,190),new Vector2(210,220) };
        var texts = new List<TMP_Text>();
        for (int i=0;i<labels.Length;i++)
        {
            var panel = new GameObject("PTBR sample " + i, typeof(RectTransform), typeof(Image), typeof(Button));
            panel.transform.SetParent(canvasObject.transform, false);
            panel.GetComponent<Image>().color = new Color(0.15f,0.20f,0.25f,1f);
            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(0,1);
            pr.pivot = new Vector2(0,1);
            pr.sizeDelta = sizes[i];
            pr.anchoredPosition = new Vector2(i<4 ? 30 : 780, -(30+(i<4 ? i*100 : (i-4)*95)));
            if (i==6) pr.anchoredPosition = new Vector2(780,-260);
            if (i==7) pr.anchoredPosition = new Vector2(1020,-260);
            var child = new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));
            child.transform.SetParent(panel.transform,false);
            var t=child.GetComponent<TextMeshProUGUI>();
            var r=t.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(8,4);r.offsetMax=new Vector2(-8,-4);
            t.font=TMP_Settings.defaultFontAsset;t.fontSize=i<6 ? 36 : 26;t.enableWordWrapping=true;t.alignment=TextAlignmentOptions.Center;t.text=labels[i];
            // Reproduce the game's late reset that defeated the v2 OnEnable adjustment.
            t.enableAutoSizing=false;
            texts.Add(t);
        }
        yield return null;
        Canvas.ForceUpdateCanvases();
        foreach(var t in texts)
        {
            t.ForceMeshUpdate();
            var bounds=t.textBounds.size;var rect=t.rectTransform.rect;
            bool fits=bounds.x<=rect.width+1f && bounds.y<=rect.height+1f;
            Debug.Log("PTBR v3 layout sample: fit="+fits+" size="+t.fontSize+" bounds="+bounds+" box="+rect.size+" text="+t.text);
        }
    }
}
