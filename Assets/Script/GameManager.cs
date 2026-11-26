using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

// Gère le score, les billes restantes et la remise en jeu de la bille.
// Rien à configurer : il se crée tout seul au lancement du jeu s'il n'est pas dans la scène.
// (Vous pouvez aussi l'ajouter sur un objet vide pour régler les valeurs dans l'Inspector.)
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    // Record séparé pour chaque flipper ("Record" pour la Forêt en feu, "Record_Foret" pour la Forêt)
    static string CleRecord => "Record" + ProfilFlipper.Suffixe;

    [Header("Partie")]
    [Tooltip("Nombre de billes par partie.")]
    public int billesParPartie = 3;
    [Tooltip("Temps (secondes) avant que la bille revienne au lanceur.")]
    public float delaiRemiseEnJeu = 1f;
    [Tooltip("Son joué quand la bille tombe dans le drain (ou sort de la table).")]
    public AudioClip sonBillePerdue;
    [Range(0f, 1f)] public float volumeBillePerdue = 0.8f;

    [Header("Bille")]
    [Tooltip("Laisser vide : trouve la bille par son tag \"Ball\" ou son nom \"la_bille\".")]
    public Rigidbody bille;
    [Tooltip("Laisser vide : la bille revient là où elle est au début du jeu (dans le couloir du lanceur).")]
    public Transform pointDeDepart;
    [Tooltip("Si la bille tombe plus bas que (départ - cette valeur), elle est perdue (tombée de la table ou dans la caisse).")]
    public float hauteurPerte = 3f;
    [Tooltip("Si la bille s'éloigne plus que cette distance de son point de départ, elle est perdue (sortie de table).")]
    public float distanceMaxTable = 60f;
    [Tooltip("Vitesse maximale de la bille (m/s). Empêche un choc trop violent de l'envoyer hors de la machine.")]
    public float vitesseMaxBille = 25f;

    [Header("Affichage")]
    public bool afficherScore = true;
    [Tooltip("Taille du texte en fraction de la hauteur de l'écran (0,022 = 24 px en 1080p).")]
    [Range(0.012f, 0.05f)]
    public float tailleRelative = 0.022f;

    public int Score { get; private set; }
    public int BilleActuelle { get; private set; } = 1;
    public int Record { get; private set; }
    public bool PartieTerminee { get; private set; }
    // Numéro de frame où la partie a été relancée (le lanceur ignore cet appui-là)
    public int FrameRejouer { get; private set; } = -1;
    // Multiplicateur de points (power-up de l'échelle d'étoiles). 1 = normal.
    public int Multiplicateur { get; set; } = 1;

    /// <summary>La bille vient d'être perdue (avant la remise en jeu).</summary>
    public event System.Action SurBillePerdue;
    /// <summary>Une nouvelle partie commence.</summary>
    public event System.Action SurNouvellePartie;
    /// <summary>La dernière bille est perdue : la partie est finie (score final en paramètre).</summary>
    public event System.Action<int> SurFinDePartie;

    // Billes en plus gagnées (2nd chance) : la prochaine bille perdue ne compte pas
    public int BillesEnPlus { get; private set; }

    /// <summary>Donne une bille en plus : la prochaine bille perdue ne fera pas avancer le compteur.</summary>
    public void AjouterBilleEnPlus() => BillesEnPlus++;

    // Menu (MenuFlipper) : tant qu'on est dans le menu, la bille est cachée et rien ne compte
    public bool EnMenu { get; private set; }
    /// <summary>Position de la bille au repos dans le lanceur.</summary>
    public Vector3 PositionDepart => positionDepart;
    public string NomJoueur { get; private set; } = "";
    bool menuPresent;

    Vector3 positionDepart;
    Quaternion rotationDepart;
    bool enRemiseEnJeu;
    Renderer[] rendusBille;
    GUIStyle style, styleGrand, styleMessage, styleSousTitre;
    int taillePolice;
    string message;
    float finMessage;
    Pinball_input controles;

    // Créé automatiquement par ProfilFlipper dans chaque scène de flipper (pas dans la scène Menu).
    public static GameManager CreerSiAbsent()
    {
        var gm = FindAnyObjectByType<GameManager>();
        return gm != null ? gm : new GameObject("GameManager").AddComponent<GameManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Record = PlayerPrefs.GetInt(CleRecord, 0);
        controles = new Pinball_input();
    }

    void OnEnable()  { controles?.Enable(); }
    void OnDisable() { controles?.Disable(); }

    void OnDestroy()
    {
        controles?.Dispose();
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (bille == null)
        {
            GameObject go = null;
            try { go = GameObject.FindWithTag("Ball"); } catch (UnityException) { }
            if (go == null) go = GameObject.Find("la_bille");
            if (go != null) bille = go.GetComponent<Rigidbody>();
        }
        if (bille == null)
        {
            Debug.LogError("GameManager : bille introuvable (tag \"Ball\" ou nom \"la_bille\").", this);
            enabled = false;
            return;
        }

        rendusBille = bille.GetComponentsInChildren<Renderer>();
        positionDepart = pointDeDepart != null ? pointDeDepart.position : bille.position;
        rotationDepart = bille.rotation;

        // Avec un menu, le jeu attend qu'on appuie sur JOUER (et qu'on ait donné son nom)
        Record = Mathf.Max(Record, TableauScores.Meilleur);
        menuPresent = FindAnyObjectByType<MenuFlipper>() != null;
        if (menuPresent)
        {
            EnMenu = true;
            Cacher(true);
        }
    }

    void FixedUpdate()
    {
        // Limite de vitesse : la bille ne peut pas être projetée hors de la machine
        if (bille == null || bille.isKinematic) return;
        if (bille.linearVelocity.sqrMagnitude > vitesseMaxBille * vitesseMaxBille)
            bille.linearVelocity = bille.linearVelocity.normalized * vitesseMaxBille;
    }

    // Recherche de bille (comme sur les vrais flippers) : si la bille ne bouge plus hors du lanceur
    // et sans être retenue par un flipper levé, on la pousse un peu pour la décoincer.
    [Header("Bille coincée")]
    [Tooltip("Secondes sans bouger avant de pousser la bille (0 = jamais).")]
    public float delaiBilleCoincee = 3f;
    [Tooltip("Vitesse donnée à la bille coincée (m/s), vers le haut de la table et le centre.")]
    public float pousseeBilleCoincee = 3f;
    Vector3 dernierePositionBille;
    float immobileDepuis;

    void VerifierBilleCoincee()
    {
        if (delaiBilleCoincee <= 0f || enRemiseEnJeu || PartieTerminee || bille.isKinematic) { immobileDepuis = Time.time; return; }
        if ((bille.position - dernierePositionBille).sqrMagnitude > 0.02f * 0.02f)
        {
            dernierePositionBille = bille.position;
            immobileDepuis = Time.time;
            return;
        }
        if (Time.time - immobileDepuis < delaiBilleCoincee) return;
        immobileDepuis = Time.time;

        // Au repos dans le lanceur, ou retenue sur un flipper : c'est normal
        if (Vector3.Distance(bille.position, positionDepart) < 1.5f) return;
        var contacts = Physics.OverlapSphere(bille.position, 0.4f, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider c in contacts)
            if (c.GetComponentInParent<FlipperBrule>() != null || c.GetComponentInParent<Flipper>() != null) return;

        string noms = string.Join(", ", System.Array.ConvertAll(contacts, c => c.name));
        Debug.LogWarning($"[Bille coincée] à ({bille.position.x:F2}, {bille.position.z:F2}), contre : {noms}. Poussée pour la décoincer.", this);
        Vector3 versCentre = new Vector3(-27.97f - bille.position.x, 0f, 0f).normalized;
        bille.linearVelocity = (Vector3.forward * 0.8f + versCentre * 0.6f).normalized * pousseeBilleCoincee + Vector3.up * 0.5f;
    }

    void Update()
    {
        if (bille == null || EnMenu) return;
        VerifierBilleCoincee();

        // Filet de sécurité : la bille est sortie de la table
        if (!enRemiseEnJeu && !PartieTerminee && !bille.isKinematic &&
            (bille.position.y < positionDepart.y - hauteurPerte ||
             Vector3.Distance(bille.position, positionDepart) > distanceMaxTable))
            BillePerdue();

        // Partie terminée sans menu : Espace / Start / bouton sud de la manette pour rejouer
        // (avec le menu, c'est l'écran de fin de partie qui propose de rejouer)
        if (PartieTerminee && !menuPresent && AppuiRejouer())
            NouvellePartie();
    }

    /// <summary>Quitte le menu et lance une partie pour ce joueur.</summary>
    public void LancerPartie(string nom)
    {
        NomJoueur = string.IsNullOrWhiteSpace(nom) ? "JOUEUR" : nom.Trim();
        EnMenu = false;
        NouvellePartie();
    }

    /// <summary>Revient au menu : la partie en cours est abandonnée (sans enregistrer de score).</summary>
    public void RetourMenu()
    {
        StopAllCoroutines();
        enRemiseEnJeu = false;
        PartieTerminee = false;
        Multiplicateur = 1;
        EnMenu = true;
        if (bille != null) Cacher(true);
    }

    // ---------------------------------------------------------------- API

    public void AjouterPoints(int points)
    {
        if (PartieTerminee || enRemiseEnJeu || EnMenu) return;
        Score += points * Mathf.Max(1, Multiplicateur);
        if (Score > Record)
        {
            Record = Score;
            PlayerPrefs.SetInt(CleRecord, Record);
        }
    }

    public void BillePerdue()
    {
        if (enRemiseEnJeu || PartieTerminee || EnMenu) return;
        Sons.Jouer(this, sonBillePerdue, volumeBillePerdue, 0f);
        SurBillePerdue?.Invoke();
        StartCoroutine(RemiseEnJeu());
    }

    public void NouvellePartie()
    {
        PlayerPrefs.Save();
        FrameRejouer = Time.frameCount;
        Score = 0;
        BilleActuelle = 1;
        PartieTerminee = false;
        Multiplicateur = 1;
        BillesEnPlus = 0;
        foreach (var groupe in FindObjectsByType<GroupeBonus>())
            groupe.Reinitialiser();
        SurNouvellePartie?.Invoke();
        PlacerBille();
    }

    // Message temporaire au centre en haut de l'écran (BOOST, BONUS...)
    public void AfficherMessage(string texte, float duree = 1.5f)
    {
        message = texte;
        finMessage = Time.time + duree;
    }

    /// <summary>Message en cours (BOOST, BONUS...) ou null. Affiché par le HUD du menu s'il y en a un.</summary>
    public string MessageActuel => !string.IsNullOrEmpty(message) && Time.time < finMessage ? message : null;

    // ---------------------------------------------------------------- interne

    IEnumerator RemiseEnJeu()
    {
        enRemiseEnJeu = true;
        Cacher(true);
        yield return new WaitForSeconds(delaiRemiseEnJeu);

        if (BillesEnPlus > 0)
        {
            // Bille en plus (2nd chance) : on rejoue la même bille
            BillesEnPlus--;
            AfficherMessage("2ND CHANCE !  BILLE EN PLUS", 2.5f);
            PlacerBille();
        }
        else if (BilleActuelle >= billesParPartie)
        {
            PartieTerminee = true;
            PlayerPrefs.Save();
            if (menuPresent) Cacher(true);   // l'écran de fin de partie prend la main
            else PlacerBille();              // la bille attend dans le lanceur
            SurFinDePartie?.Invoke(Score);
        }
        else
        {
            BilleActuelle++;
            PlacerBille();
        }
        enRemiseEnJeu = false;
    }

    void PlacerBille()
    {
        bille.isKinematic = false;
        bille.linearVelocity = Vector3.zero;
        bille.angularVelocity = Vector3.zero;
        bille.position = positionDepart;
        bille.rotation = rotationDepart;
        bille.transform.SetPositionAndRotation(positionDepart, rotationDepart);

        var trail = bille.GetComponent<TrailRenderer>();
        if (trail != null) trail.Clear();

        Cacher(false);
    }

    void Cacher(bool cacher)
    {
        if (cacher)
        {
            bille.linearVelocity = Vector3.zero;
            bille.angularVelocity = Vector3.zero;
            bille.isKinematic = true;   // ne bouge plus pendant l'attente
        }
        foreach (var r in rendusBille)
            if (r != null) r.enabled = !cacher;
    }

    bool AppuiRejouer()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (controles != null && controles.inputs.start.WasPressedThisFrame())
            || (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
            || (g != null && (g.startButton.wasPressedThisFrame || g.buttonSouth.wasPressedThisFrame));
    }

    void OnGUI()
    {
        // Avec le menu, c'est son HUD (gros score en haut de l'écran) qui affiche tout
        if (!afficherScore || EnMenu || menuPresent) return;

        // Taille du texte proportionnelle à la hauteur de l'écran (pareil en 720p, 1080p, 4K...)
        int taille = Mathf.Max(12, Mathf.RoundToInt(Screen.height * tailleRelative));
        if (style == null || taillePolice != taille)
            CreerStyles(taille);

        // Bandeau compact sur une seule ligne, en haut à gauche
        string infos = (NomJoueur.Length > 0 ? $"<b>{NomJoueur}</b>     " : "") + $"SCORE <b>{Score:N0}</b>     BALLE <b>{Mathf.Min(BilleActuelle, billesParPartie)}/{billesParPartie}</b>     RECORD <b>{Record:N0}</b>";
        if (Multiplicateur > 1) infos += $"     <b>x{Multiplicateur}</b>";
        if (BillesEnPlus > 0) infos += $"     <b>+{BillesEnPlus} BILLE</b>";
        Vector2 dim = style.CalcSize(new GUIContent(infos));
        float marge = taille * 0.5f;
        var fond = new Rect(taille * 0.5f, taille * 0.4f, dim.x + marge * 2f, dim.y + marge * 0.6f);
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(fond, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(fond.x + marge, fond.y + marge * 0.3f, dim.x, dim.y), infos, style);

        // Message temporaire (BOOST, BONUS...)
        if (!string.IsNullOrEmpty(message) && Time.time < finMessage)
            Ombre(new Rect(0, Screen.height * 0.14f, Screen.width, taille * 2.5f), message, styleMessage);

        if (PartieTerminee && !menuPresent)
        {
            var r = new Rect(0, Screen.height * 0.5f - taille * 3f, Screen.width, taille * 3.5f);
            Ombre(r, "GAME OVER", styleGrand);
            r.y += taille * 3.2f;
            r.height = taille * 2f;
            Ombre(r, "Appuyez sur ESPACE pour rejouer", styleSousTitre);
        }
    }

    void CreerStyles(int taille)
    {
        taillePolice = taille;
        style = new GUIStyle(GUI.skin.label) { fontSize = taille, richText = true, wordWrap = false };
        style.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
        styleMessage = new GUIStyle(style) { fontSize = Mathf.RoundToInt(taille * 1.6f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        styleMessage.normal.textColor = new Color(1f, 0.75f, 0.2f);
        styleGrand = new GUIStyle(style) { fontSize = taille * 3, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        styleSousTitre = new GUIStyle(style) { alignment = TextAnchor.MiddleCenter };
    }

    static void Ombre(Rect r, string texte, GUIStyle s)
    {
        var c = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, 0.7f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), texte, s);
        s.normal.textColor = c;
        GUI.Label(r, texte, s);
    }
}
