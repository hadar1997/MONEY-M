#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class UIGenerator : MonoBehaviour
{
    [MenuItem("Tools/Generate Map & UI")]
    public static void CreateUI()
    {
        Canvas existingCanvas = Object.FindAnyObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            Undo.DestroyObjectImmediate(existingCanvas.gameObject);
        }

        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        canvasGO.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // כותרת כסף
        GameObject moneyTextGO = new GameObject("MoneyText");
        moneyTextGO.transform.SetParent(canvasGO.transform, false);
        Text moneyText = moneyTextGO.AddComponent<Text>();
        moneyText.text = "כסף: $50,000";
        moneyText.fontSize = 45;
        moneyText.alignment = TextAnchor.MiddleCenter;
        moneyText.color = Color.yellow;
        moneyText.font = font;

        RectTransform moneyRect = moneyTextGO.GetComponent<RectTransform>();
        moneyRect.anchorMin = new Vector2(0.5f, 1f);
        moneyRect.anchorMax = new Vector2(0.5f, 1f);
        moneyRect.pivot = new Vector2(0.5f, 1f);
        moneyRect.anchoredPosition = new Vector2(0, -60);
        moneyRect.sizeDelta = new Vector2(700, 90);

        // אזור המפה עם גריד
        GameObject mapGO = new GameObject("MapArea");
        mapGO.transform.SetParent(canvasGO.transform, false);
        Image mapImg = mapGO.AddComponent<Image>();
        mapImg.color = new Color(0.12f, 0.15f, 0.18f);

        RectTransform mapRect = mapGO.GetComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.pivot = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = new Vector2(0, 130);
        mapRect.sizeDelta = new Vector2(1000, 950);

        GridLayoutGroup grid = mapGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(225, 170);
        grid.spacing = new Vector2(8, 8);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        // צבעים: מחום (זול) לזהב (יקר)
        Color cheapColor = new Color(0.5f, 0.33f, 0.2f);
        Color expensiveColor = new Color(1f, 0.84f, 0.2f);

        string[] names = {
            "מחסן ישן", "סטודיו", "דירת חדר", "דירת 2 חד'", "דירת 3 חד'",
            "דירת 4 חד'", "דירת גן", "פנטהאוז", "בית טורי", "בית קטן",
            "בית גדול", "וילה", "וילה יוקרה", "דופלקס", "בניין קטן",
            "מגדל מגורים", "מגדל יוקרה", "קניון", "מלון בוטיק", "גורד שחקים"
        };

        for (int i = 0; i < names.Length; i++)
        {
            float t = i / (float)(names.Length - 1);

            GameObject houseGO = new GameObject($"House_{i}");
            houseGO.transform.SetParent(mapGO.transform, false);

            Image bg = houseGO.AddComponent<Image>();
            bg.color = Color.Lerp(cheapColor, expensiveColor, t);
            houseGO.AddComponent<Button>();

            // שם הנכס
            GameObject nameGO = new GameObject("NameLabel");
            nameGO.transform.SetParent(houseGO.transform, false);
            Text nameTxt = nameGO.AddComponent<Text>();
            nameTxt.text = names[i];
            nameTxt.fontSize = 20;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.font = font;
            nameTxt.color = Color.white;
            RectTransform nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.45f);
            nameRect.anchorMax = new Vector2(1, 1f);
            nameRect.sizeDelta = Vector2.zero;

            // מחיר נוכחי
            GameObject priceGO = new GameObject("PriceLabel");
            priceGO.transform.SetParent(houseGO.transform, false);
            Text priceTxt = priceGO.AddComponent<Text>();
            priceTxt.text = "$0";
            priceTxt.fontSize = 22;
            priceTxt.fontStyle = FontStyle.Bold;
            priceTxt.alignment = TextAnchor.MiddleCenter;
            priceTxt.font = font;
            priceTxt.color = Color.white;
            RectTransform priceRect = priceGO.GetComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0, 0f);
            priceRect.anchorMax = new Vector2(1, 0.45f);
            priceRect.sizeDelta = Vector2.zero;

            // שכבת "בבעלותך" (מוסתרת כברירת מחדל)
            GameObject overlayGO = new GameObject("OwnedOverlay");
            overlayGO.transform.SetParent(houseGO.transform, false);
            Image overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.color = new Color(0.2f, 1f, 0.3f, 0.55f);
            RectTransform overlayRect = overlayGO.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;
            overlayGO.SetActive(false);
        }

        // פאנל פרטים למטה
        GameObject panelGO = new GameObject("InfoPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0, 30);
        panelRect.sizeDelta = new Vector2(1000, 330);

        GameObject infoTxtGO = new GameObject("InfoPanelText");
        infoTxtGO.transform.SetParent(panelGO.transform, false);
        Text infoTxt = infoTxtGO.AddComponent<Text>();
        infoTxt.text = "לחץ על בית במפה כדי לצפות בפרטים";
        infoTxt.fontSize = 28;
        infoTxt.alignment = TextAnchor.MiddleCenter;
        infoTxt.font = font;
        infoTxt.color = Color.white;

        RectTransform infoTxtRect = infoTxtGO.GetComponent<RectTransform>();
        infoTxtRect.anchorMin = new Vector2(0, 0.35f);
        infoTxtRect.anchorMax = new Vector2(1, 1f);
        infoTxtRect.sizeDelta = Vector2.zero;

        CreateButton(panelGO.transform, "BuyButton", "קנה נכס", new Vector2(-230, -100), new Color(0.2f, 0.7f, 0.3f), font);
        CreateButton(panelGO.transform, "SellButton", "מכור נכס", new Vector2(230, -100), new Color(0.8f, 0.3f, 0.2f), font);

        Selection.activeGameObject = canvasGO;
        Debug.Log("מפה עם 20 נכסים נוצרה בהצלחה!");
    }

    private static void CreateButton(Transform parent, string name, string label, Vector2 pos, Color btnColor, Font font)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);
        Image img = btnGO.AddComponent<Image>();
        img.color = btnColor;
        btnGO.AddComponent<Button>();

        RectTransform rect = btnGO.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(350, 90);

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(btnGO.transform, false);
        Text txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = 32;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = font;
        txt.color = Color.white;

        RectTransform txtRect = txtGO.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
    }
}
#endif