using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Écran de choix du flipper (scène "Menu", la première du jeu) : FORÊT ou FORÊT EN FEU,
// avec les auteurs et le record de chacun, et un écran HIGH SCORES qui montre les deux top 10 côte à côte.
// Rien à configurer : créé tout seul par ProfilFlipper, l'interface est construite par code.
public class SelecteurFlipper : MonoBehaviour
{
    static readonly Color Braise = new Color(1f, 0.478f, 0.118f);
    static readonly Color Sapin = new Color(0.45f, 0.85f, 0.45f);
    static readonly Color FondPanneau = new Color(0.05f, 0.06f, 0.10f, 0.94f);
    static readonly Color FondBouton = new Color(0.13f, 0.10f, 0.10f, 0.95f);

    GameObject ecranChoix, ecranScores;
    Button premierChoix, premierScores;
    TMP_Text scoresForet, scoresForetBrulee;

    public static void CreerSiAbsent()
    {
        if (FindAnyObjectByType<SelecteurFlipper>() == null)
            new GameObject("Selecteur_Flipper").AddComponent<SelecteurFlipper>();
    }

    void Awake()
    {
        Time.timeScale = 1f;
        if (Camera.main == null)
        {
            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
        }
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        Construire();
        AfficherChoix();
    }

    void Update()
    {
        // Échap / Select : retour de l'écran des scores
        var k = Keyboard.current;
        var g = Gamepad.current;
        bool retour = (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && (g.selectButton.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame));
        if (retour && ecranScores.activeSelf) AfficherChoix();
    }

    void AfficherChoix()
    {
        ecranScores.SetActive(false);
        ecranChoix.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(premierChoix.gameObject);
    }

    void AfficherScores()
    {
        scoresForet.text = Classement(TableauScores.LignesDe(ProfilFlipper.SuffixeForet));
        scoresForetBrulee.text = Classement(TableauScores.LignesDe(ProfilFlipper.SuffixeForetBrulee));
        ecranChoix.SetActive(false);
        ecranScores.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(premierScores.gameObject);
    }

    static string Classement(List<TableauScores.Ligne> lignes)
    {
        if (lignes.Count == 0) return "<align=center><color=#FFFFFF80>Aucun score pour l'instant.</color></align>";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lignes.Count; i++)
        {
            string debut = i == 0 ? "<color=#FFD24A>" : "";
            string fin = i == 0 ? "</color>" : "";
            sb.Append($"{debut}<pos=0%>{i + 1}.<pos=12%>{lignes[i].nom}<pos=62%>{lignes[i].score:N0}{fin}\n");
        }
        return sb.ToString();
    }

    static string Record(string suffixe)
    {
        var l = TableauScores.LignesDe(suffixe);
        return l.Count > 0 ? $"RECORD {l[0].score:N0} ({l[0].nom})" : "Pas encore de record";
    }

    void Construire()
    {
        var canvasGo = new GameObject("Canvas_Choix", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // --- Choix du flipper
        ecranChoix = Panneau(canvasGo.transform, "Ecran_Choix", 900);
        var c = ecranChoix.transform;
        Texte(c, "FLIPPER", 84, Braise, FontStyles.Bold, 105);
        Texte(c, "Choisis ta forêt", 32, new Color(1f, 1f, 1f, 0.75f), FontStyles.Italic, 46);

        premierChoix = Bouton(c, "FORÊT", $"par {ProfilFlipper.AuteursForet}\n{Record(ProfilFlipper.SuffixeForet)}", Sapin,
                              () => SceneManager.LoadScene(ProfilFlipper.SceneForet), 160);
        Bouton(c, "FORÊT EN FEU", $"par {ProfilFlipper.AuteursForetBrulee}\n{Record(ProfilFlipper.SuffixeForetBrulee)}", Braise,
               () => SceneManager.LoadScene(ProfilFlipper.SceneForetBrulee), 160);
        Bouton(c, "HIGH SCORES", "", new Color(1f, 0.82f, 0.29f), AfficherScores, 80);
        Bouton(c, "QUITTER", "", new Color(1f, 1f, 1f, 0.8f), Quitter, 80);

        // --- Les deux top 10 côte à côte
        ecranScores = Panneau(canvasGo.transform, "Ecran_Scores", 1500);
        var s = ecranScores.transform;
        Texte(s, "HIGH SCORES", 70, Braise, FontStyles.Bold, 95);

        var colonnes = new GameObject("Colonnes", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        colonnes.transform.SetParent(s, false);
        colonnes.GetComponent<LayoutElement>().preferredHeight = 560;
        var hl = colonnes.GetComponent<HorizontalLayoutGroup>();
        hl.spacing = 50;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = hl.childForceExpandHeight = true;
        scoresForet = Colonne(colonnes.transform, "FORÊT", ProfilFlipper.AuteursForet, Sapin);
        scoresForetBrulee = Colonne(colonnes.transform, "FORÊT EN FEU", ProfilFlipper.AuteursForetBrulee, Braise);

        premierScores = Bouton(s, "RETOUR", "", new Color(1f, 1f, 1f, 0.8f), AfficherChoix, 80);
    }

    static GameObject Panneau(Transform parent, string nom, float largeur)
    {
        var go = new GameObject(nom, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = FondPanneau;
        ((RectTransform)go.transform).sizeDelta = new Vector2(largeur, 0);
        var vl = go.GetComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(60, 60, 44, 48);
        vl.spacing = 20;
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var bord = go.AddComponent<Outline>();
        bord.effectColor = new Color(Braise.r, Braise.g, Braise.b, 0.6f);
        bord.effectDistance = new Vector2(3, -3);
        go.SetActive(false);
        return go;
    }

    static TMP_Text Colonne(Transform parent, string titre, string auteurs, Color couleur)
    {
        var go = new GameObject("Colonne_" + titre, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
        var vl = go.GetComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(30, 30, 20, 20);
        vl.spacing = 6;
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        var bord = go.AddComponent<Outline>();
        bord.effectColor = new Color(couleur.r, couleur.g, couleur.b, 0.6f);
        bord.effectDistance = new Vector2(2, -2);

        Texte(go.transform, titre, 44, couleur, FontStyles.Bold, 56);
        Texte(go.transform, auteurs, 22, new Color(1f, 1f, 1f, 0.6f), FontStyles.Italic, 32);
        var liste = Texte(go.transform, "", 30, Color.white, FontStyles.Normal, 420);
        liste.alignment = TextAlignmentOptions.TopLeft;
        liste.richText = true;
        liste.lineSpacing = 6f;
        return liste;
    }

    static void Quitter()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static TMP_Text Texte(Transform parent, string texte, float taille, Color couleur, FontStyles style, float hauteur)
    {
        var go = new GameObject("Texte", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = hauteur;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = texte; t.fontSize = taille; t.color = couleur; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    static Button Bouton(Transform parent, string titre, string detail, Color couleur, UnityEngine.Events.UnityAction action, float hauteur = 130)
    {
        var go = new GameObject("Bouton_" + titre, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = hauteur;
        go.GetComponent<Image>().color = FondBouton;
        var b = go.GetComponent<Button>();
        var c = b.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1.6f, 1.2f, 1f);
        c.selectedColor = new Color(1.6f, 1.2f, 1f);
        c.pressedColor = new Color(0.8f, 0.6f, 0.5f);
        c.colorMultiplier = 1.6f;
        b.colors = c;
        b.onClick.AddListener(action);
        var bord = go.AddComponent<Outline>();
        bord.effectColor = new Color(couleur.r, couleur.g, couleur.b, 0.7f);
        bord.effectDistance = new Vector2(2, -2);

        var t = new GameObject("Texte", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(go.transform, false);
        var trt = (RectTransform)t.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        t.text = string.IsNullOrEmpty(detail)
            ? $"<b>{titre}</b>"
            : $"<size=46><b>{titre}</b></size>\n<size=24><color=#FFFFFFB0>{detail}</color></size>";
        t.fontSize = 40; t.color = couleur;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return b;
    }
}
