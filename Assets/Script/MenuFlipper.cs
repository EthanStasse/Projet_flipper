using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu du flipper : JOUER (on entre son nom d'abord), HIGH SCORES (top 10), QUITTER.
// Écran de fin de partie (score, rang dans le top 10, REJOUER / HIGH SCORES / MENU)
// et pause avec Échap pendant la partie (REPRENDRE / MENU / QUITTER).
// Rien à configurer : il se crée tout seul au lancement (comme le GameManager), l'interface est construite par code.
public class MenuFlipper : MonoBehaviour
{
    public string titre = "FORÊT EN FEU";
    public string sousTitre = "FLIPPER";
    [Tooltip("Nombre de caractères maximum du nom.")]
    public int longueurNom = 12;

    static readonly Color Braise = new Color(1f, 0.478f, 0.118f);          // #FF7A1E
    static readonly Color FondEcran = new Color(0.02f, 0.03f, 0.06f, 0.72f);
    static readonly Color FondPanneau = new Color(0.05f, 0.06f, 0.10f, 0.94f);
    static readonly Color FondBouton = new Color(0.13f, 0.10f, 0.10f, 0.95f);
    const string CleDernierNom = "DernierNom";

    enum Ecran { Aucun, Principal, Nom, Scores, FinPartie, Pause }

    readonly Dictionary<Ecran, GameObject> ecrans = new Dictionary<Ecran, GameObject>();
    readonly Dictionary<Ecran, Selectable> premierChoix = new Dictionary<Ecran, Selectable>();
    GameObject fond;
    TMP_InputField champNom;
    Button boutonJouerNom;
    TMP_Text texteScores, texteFin;
    Ecran ecran = Ecran.Aucun, retourDesScores = Ecran.Principal;
    /// <summary>Épaisseur du bandeau du score (unités du canvas, référence 1920 x 1080). La caméra lui laisse cette place.</summary>
    public const float HauteurHud = 120f;
    GameObject hud;
    TMP_Text hudNom, hudScore, hudBille, hudBonus, hudMessage;
    int dernierRang;
    GameManager abonne;

    // Créé automatiquement par ProfilFlipper dans chaque scène de flipper (pas dans la scène Menu).
    public static void CreerSiAbsent()
    {
        if (FindAnyObjectByType<MenuFlipper>() == null)
            new GameObject("Menu").AddComponent<MenuFlipper>();
    }

    void Awake()
    {
        titre = ProfilFlipper.Titre;
        Construire();
    }

    void Start()
    {
        abonne = GameManager.Instance;
        if (abonne != null) abonne.SurFinDePartie += FinDePartie;
        Afficher(Ecran.Principal);
    }

    void OnDestroy()
    {
        if (abonne != null) abonne.SurFinDePartie -= FinDePartie;
        Time.timeScale = 1f;
    }

    void Update()
    {
        MettreAJourHud();
        var k = Keyboard.current;
        var g = Gamepad.current;
        bool retour = (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.selectButton.wasPressedThisFrame);
        if (!retour) return;

        var gm = GameManager.Instance;
        switch (ecran)
        {
            case Ecran.Aucun:
                if (gm != null && !gm.EnMenu && !gm.PartieTerminee) Afficher(Ecran.Pause);
                break;
            case Ecran.Pause: Afficher(Ecran.Aucun); break;
            case Ecran.Nom: Afficher(Ecran.Principal); break;
            case Ecran.Scores: Afficher(retourDesScores); break;
        }
    }

    // Bandeau en haut de l'écran pendant la partie : nom, gros score, bille, bonus, messages
    void MettreAJourHud()
    {
        var gm = GameManager.Instance;
        bool visible = gm != null && !gm.EnMenu && (ecran == Ecran.Aucun || ecran == Ecran.Pause);
        if (hud.activeSelf != visible) hud.SetActive(visible);
        if (!visible) return;

        hudNom.text = gm.NomJoueur;
        hudScore.text = gm.Score.ToString("N0");
        hudBille.text = $"BILLE <b>{Mathf.Min(gm.BilleActuelle, gm.billesParPartie)}</b> / {gm.billesParPartie}\n<size=70%>RECORD {Mathf.Max(gm.Record, gm.Score):N0}</size>";
        string bonus = "";
        if (gm.Multiplicateur > 1) bonus += $"<color=#FFD24A>POINTS x{gm.Multiplicateur}</color>   ";
        if (gm.BillesEnPlus > 0) bonus += $"<color=#7CFF8A>2ND CHANCE +{gm.BillesEnPlus}</color>";
        hudBonus.text = bonus;
        hudMessage.text = gm.MessageActuel ?? "";
    }

    // ==================================================================== ACTIONS

    void Afficher(Ecran e)
    {
        ecran = e;
        foreach (var paire in ecrans) paire.Value.SetActive(paire.Key == e);
        fond.SetActive(e != Ecran.Aucun);
        Time.timeScale = e == Ecran.Pause ? 0f : 1f;

        if (e == Ecran.Scores) RemplirScores();
        if (e == Ecran.Nom)
        {
            champNom.text = PlayerPrefs.GetString(CleDernierNom, "");
            boutonJouerNom.interactable = champNom.text.Trim().Length > 0;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (premierChoix.TryGetValue(e, out Selectable s) && s != null)
            {
                EventSystem.current.SetSelectedGameObject(s.gameObject);
                if (s == champNom) champNom.ActivateInputField();
            }
        }
    }

    void LancerAvecNom()
    {
        string nom = champNom.text.Trim();
        if (nom.Length == 0) return;
        PlayerPrefs.SetString(CleDernierNom, nom);
        PlayerPrefs.Save();
        Afficher(Ecran.Aucun);
        GameManager.Instance?.LancerPartie(nom);
        // Flipper avec deux vues (Forêt en feu) : rappel rapide de la touche C au début de la partie
        if (FindAnyObjectByType<CameraFlipper>() != null)
            GameManager.Instance?.AfficherMessage("TOUCHE C : CHANGER DE VUE", 2.5f);
    }

    void Rejouer()
    {
        Afficher(Ecran.Aucun);
        var gm = GameManager.Instance;
        gm?.LancerPartie(gm.NomJoueur);
    }

    void RetourAuMenu()
    {
        GameManager.Instance?.RetourMenu();
        Afficher(Ecran.Principal);
    }

    void VoirScores(Ecran depuis)
    {
        retourDesScores = depuis;
        Afficher(Ecran.Scores);
    }

    // Retour à l'écran de choix (Forêt / Forêt en feu)
    static void ChangerDeFlipper()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(ProfilFlipper.SceneMenu);
    }

    static void Quitter()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void FinDePartie(int score)
    {
        var gm = GameManager.Instance;
        string nom = gm != null ? gm.NomJoueur : "JOUEUR";
        dernierRang = TableauScores.Ajouter(nom, score);
        string resultat = dernierRang == 1 ? "<color=#FFD24A>NOUVEAU RECORD !</color>"
                        : dernierRang > 0 ? $"{dernierRang}{(dernierRang == 1 ? "er" : "e")} au classement"
                        : "Pas dans le top 10, encore un effort !";
        texteFin.text = $"<size=130%><b>{nom}</b></size>\n<size=170%><color=#FF7A1E><b>{score:N0}</b></color></size>\n{resultat}";
        Afficher(Ecran.FinPartie);
    }

    void RemplirScores()
    {
        var lignes = TableauScores.Lignes;
        if (lignes.Count == 0)
        {
            texteScores.text = "<align=center>Aucun score pour l'instant.\nÀ toi de jouer !</align>";
            return;
        }
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lignes.Count; i++)
        {
            bool nouveau = retourDesScores == Ecran.FinPartie && i + 1 == dernierRang;
            string couleur = nouveau ? "<color=#FFD24A>" : i == 0 ? "<color=#FF7A1E>" : "";
            string fin = couleur.Length > 0 ? "</color>" : "";
            sb.Append($"{couleur}<pos=0%>{i + 1}.<pos=11%>{lignes[i].nom}<pos=60%>{lignes[i].score,12:N0}<pos=86%><size=70%>{lignes[i].date}</size>{fin}\n");
        }
        texteScores.text = sb.ToString();
    }

    // ==================================================================== CONSTRUCTION DE L'INTERFACE

    void Construire()
    {
        // Système d'événements (clavier, manette, souris) s'il n'y en a pas
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            es.transform.SetParent(transform, false);
        }

        var canvasGo = new GameObject("Canvas_Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        ConstruireHud(canvasGo.transform);
        fond = Panneau(canvasGo.transform, "Fond", FondEcran, Vector2.zero, true);

        // --- Principal
        var p = Ecran_(canvasGo.transform, Ecran.Principal, 720);
        Texte(p, titre, 78, Braise, FontStyles.Bold, 100);
        Texte(p, sousTitre, 34, new Color(1f, 1f, 1f, 0.75f), FontStyles.Normal, 50).characterSpacing = 30f;
        Texte(p, "par " + ProfilFlipper.Auteurs, 24, new Color(1f, 1f, 1f, 0.6f), FontStyles.Italic, 34);
        Espace(p, 30);
        premierChoix[Ecran.Principal] = Bouton(p, "JOUER", () => Afficher(Ecran.Nom));
        Bouton(p, "HIGH SCORES", () => VoirScores(Ecran.Principal));
        Bouton(p, "CHANGER DE FLIPPER", ChangerDeFlipper);
        Bouton(p, "QUITTER", Quitter);
        Texte(p, "Flippers : flèches ou Q / D     Lanceur : Espace\nC : changer de vue     Échap : pause", 22, new Color(1f, 1f, 1f, 0.55f), FontStyles.Italic, 60);

        // --- Nom du joueur
        var n = Ecran_(canvasGo.transform, Ecran.Nom, 560);
        Texte(n, "TON NOM", 56, Braise, FontStyles.Bold, 80);
        Texte(n, "Il apparaîtra dans les high scores", 26, new Color(1f, 1f, 1f, 0.7f), FontStyles.Italic, 40);
        Espace(n, 10);
        champNom = ChampTexte(n);
        premierChoix[Ecran.Nom] = champNom;
        boutonJouerNom = Bouton(n, "JOUER", LancerAvecNom);
        Bouton(n, "RETOUR", () => Afficher(Ecran.Principal));

        // --- High scores
        var s = Ecran_(canvasGo.transform, Ecran.Scores, 860);
        Texte(s, "HIGH SCORES", 60, Braise, FontStyles.Bold, 86);
        texteScores = Texte(s, "", 32, Color.white, FontStyles.Normal, 520);
        texteScores.alignment = TextAlignmentOptions.TopLeft;
        texteScores.lineSpacing = 12f;
        premierChoix[Ecran.Scores] = Bouton(s, "RETOUR", () => Afficher(retourDesScores));

        // --- Fin de partie
        var f = Ecran_(canvasGo.transform, Ecran.FinPartie, 860);
        Texte(f, "GAME OVER", 80, Braise, FontStyles.Bold, 110);
        texteFin = Texte(f, "", 40, Color.white, FontStyles.Normal, 230);
        premierChoix[Ecran.FinPartie] = Bouton(f, "REJOUER", Rejouer);
        Bouton(f, "HIGH SCORES", () => VoirScores(Ecran.FinPartie));
        Bouton(f, "MENU", RetourAuMenu);
        Bouton(f, "CHANGER DE FLIPPER", ChangerDeFlipper);

        // --- Pause
        var pa = Ecran_(canvasGo.transform, Ecran.Pause, 660);
        Texte(pa, "PAUSE", 70, Braise, FontStyles.Bold, 100);
        premierChoix[Ecran.Pause] = Bouton(pa, "REPRENDRE", () => Afficher(Ecran.Aucun));
        Bouton(pa, "MENU", RetourAuMenu);
        Bouton(pa, "CHANGER DE FLIPPER", ChangerDeFlipper);
        Bouton(pa, "QUITTER", Quitter);
    }

    void ConstruireHud(Transform canvas)
    {
        // Bandeau sombre en haut, sur toute la largeur
        hud = Panneau(canvas, "HUD", new Color(0.02f, 0.03f, 0.06f, 0.78f), Vector2.zero, false);
        var rt = (RectTransform)hud.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, HauteurHud);
        rt.anchoredPosition = Vector2.zero;
        var bord = hud.AddComponent<Outline>();
        bord.effectColor = new Color(Braise.r, Braise.g, Braise.b, 0.7f);
        bord.effectDistance = new Vector2(0f, -3f);

        hudNom = TexteHud(hud.transform, new Vector2(0f, 0f), new Vector2(0.3f, 1f), 40, Braise, TextAlignmentOptions.MidlineLeft);
        hudScore = TexteHud(hud.transform, new Vector2(0.3f, 0.12f), new Vector2(0.7f, 1f), 86, Color.white, TextAlignmentOptions.Center);
        hudScore.fontStyle = FontStyles.Bold;
        hudBonus = TexteHud(hud.transform, new Vector2(0.3f, -0.05f), new Vector2(0.7f, 0.3f), 26, Color.white, TextAlignmentOptions.Center);
        hudBille = TexteHud(hud.transform, new Vector2(0.7f, 0f), new Vector2(1f, 1f), 36, Color.white, TextAlignmentOptions.MidlineRight);
        hudNom.fontStyle = FontStyles.Bold;

        // Messages (BOOST, BONUS, 2ND CHANCE...) sous le bandeau
        hudMessage = TexteHud(hud.transform, new Vector2(0f, -1.1f), new Vector2(1f, -0.1f), 54, new Color(1f, 0.75f, 0.2f), TextAlignmentOptions.Center);
        hudMessage.fontStyle = FontStyles.Bold;
        hudMessage.outlineWidth = 0.2f;
        hudMessage.outlineColor = new Color32(0, 0, 0, 200);
        hud.SetActive(false);
    }

    static TMP_Text TexteHud(Transform parent, Vector2 ancreMin, Vector2 ancreMax, float taille, Color couleur, TextAlignmentOptions alignement)
    {
        var go = new GameObject("Texte", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = ancreMin;
        rt.anchorMax = ancreMax;
        rt.offsetMin = new Vector2(40f, 0f);
        rt.offsetMax = new Vector2(-40f, 0f);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = taille;
        t.color = couleur;
        t.alignment = alignement;
        t.richText = true;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    Transform Ecran_(Transform parent, Ecran e, float largeur)
    {
        GameObject go = Panneau(parent, $"Ecran_{e}", FondPanneau, new Vector2(largeur, 0f), false);
        var bord = go.AddComponent<Outline>();
        bord.effectColor = new Color(Braise.r, Braise.g, Braise.b, 0.6f);
        bord.effectDistance = new Vector2(2f, -2f);
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(50, 50, 40, 44);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        go.SetActive(false);
        ecrans[e] = go;
        return go.transform;
    }

    static GameObject Panneau(Transform parent, string nom, Color couleur, Vector2 taille, bool pleinEcran)
    {
        var go = new GameObject(nom, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        if (pleinEcran)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = taille;
        }
        go.GetComponent<Image>().color = couleur;
        return go;
    }

    static TMP_Text Texte(Transform parent, string texte, float taille, Color couleur, FontStyles style, float hauteur)
    {
        var go = new GameObject("Texte", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = texte;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.richText = true;
        t.raycastTarget = false;
        go.GetComponent<LayoutElement>().preferredHeight = hauteur;
        return t;
    }

    static void Espace(Transform parent, float hauteur)
    {
        var go = new GameObject("Espace", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = hauteur;
    }

    static Button Bouton(Transform parent, string texte, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject($"Bouton_{texte}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 78f;
        var img = go.GetComponent<Image>();
        img.color = Color.white;
        var b = go.GetComponent<Button>();
        var c = b.colors;
        c.normalColor = FondBouton;
        c.highlightedColor = Color.Lerp(FondBouton, Braise, 0.75f);
        c.selectedColor = Color.Lerp(FondBouton, Braise, 0.75f);
        c.pressedColor = Braise;
        c.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);
        c.fadeDuration = 0.08f;
        b.colors = c;
        b.onClick.AddListener(action);
        var bord = go.AddComponent<Outline>();
        bord.effectColor = new Color(Braise.r, Braise.g, Braise.b, 0.8f);
        bord.effectDistance = new Vector2(2f, -2f);

        var t = Texte(go.transform, texte, 36, Color.white, FontStyles.Bold, 78f);
        var rt = (RectTransform)t.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        t.characterSpacing = 8f;
        return b;
    }

    TMP_InputField ChampTexte(Transform parent)
    {
        // Créé désactivé : TMP_InputField a besoin de ses textes avant d'être activé
        var go = new GameObject("ChampNom");
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<LayoutElement>().preferredHeight = 86f;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        var bord = go.AddComponent<Outline>();
        bord.effectColor = Braise;
        bord.effectDistance = new Vector2(2f, -2f);

        var zone = new GameObject("Zone", typeof(RectTransform), typeof(RectMask2D));
        zone.transform.SetParent(go.transform, false);
        var rz = (RectTransform)zone.transform;
        rz.anchorMin = Vector2.zero;
        rz.anchorMax = Vector2.one;
        rz.offsetMin = new Vector2(20, 8);
        rz.offsetMax = new Vector2(-20, -8);

        TMP_Text texte = Texte(zone.transform, "", 44, Color.white, FontStyles.Bold, 70);
        TMP_Text indice = Texte(zone.transform, "Entre ton nom…", 40, new Color(1f, 1f, 1f, 0.35f), FontStyles.Italic, 70);
        foreach (TMP_Text t in new[] { texte, indice })
        {
            Destroy(t.GetComponent<LayoutElement>());
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        var champ = go.AddComponent<TMP_InputField>();
        champ.textViewport = rz;
        champ.textComponent = texte;
        champ.placeholder = indice;
        champ.characterLimit = longueurNom;
        champ.customCaretColor = true;
        champ.caretColor = Braise;
        champ.caretWidth = 3;
        champ.selectionColor = new Color(Braise.r, Braise.g, Braise.b, 0.4f);
        champ.targetGraphic = img;
        // Lettres, chiffres, espace, - et _ ; en majuscules (comme sur une borne)
        champ.onValidateInput = (s, i, ch) => char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_' ? char.ToUpperInvariant(ch) : '\0';
        champ.onValueChanged.AddListener(v => { if (boutonJouerNom != null) boutonJouerNom.interactable = v.Trim().Length > 0; });
        champ.onSubmit.AddListener(_ => LancerAvecNom());
        go.SetActive(true);
        return champ;
    }
}
