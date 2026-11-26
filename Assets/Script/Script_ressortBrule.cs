using UnityEngine;
using UnityEngine.InputSystem;

// Lanceur (plunger) du flipper.
// - Maintenir Espace / bouton "start" de Pinball_input / bouton sud de la manette : le ressort recule et la puissance se charge.
// - Relâcher : le ressort revient d'un coup et, si la bille est dans la zone du lanceur, elle reçoit une impulsion.
// La direction du tir est l'axe bleu (Z) de la Zone_Lanceur.
[RequireComponent(typeof(Rigidbody))]
public class Script_ressortBrule : MonoBehaviour
{
    [Header("Références (remplies par Flipper > Installer)")]
    [Tooltip("Zone trigger dans le couloir du lanceur. Son axe bleu (Z) donne la direction du tir.")]
    public ZoneLanceur zoneLanceur;
    [Tooltip("Laisser vide : la bille est trouvée par son tag \"Ball\" ou son nom \"la_bille\".")]
    public Rigidbody bille;

    [Header("Puissance du tir (vitesse donnée à la bille, en m/s)")]
    [Tooltip("Vitesse pour un simple appui court.")]
    public float vitesseMin = 13f;
    [Tooltip("Vitesse quand la charge est à 100 %.")]
    public float vitesseMax = 20f;
    [Tooltip("Temps (secondes) de maintien pour atteindre 100 % de charge.")]
    public float tempsChargeMax = 1.2f;

    [Header("Direction de secours (si aucune Zone Lanceur)")]
    public Vector3 directionTirSecours = new Vector3(0f, 0.31f, 0.95f);
    [Tooltip("Sans zone : distance max entre la bille et le ressort pour pouvoir tirer.")]
    public float distanceDetectionSecours = 1.5f;

    [Header("Animation du ressort")]
    [Tooltip("Distance de recul du ressort à pleine charge.")]
    public float distanceRecul = 0.5f;
    [Tooltip("Vitesse (m/s) du ressort quand il revient en place après le tir.")]
    public float vitesseRetour = 25f;

    [Header("Son")]
    public AudioSource audioSource;
    public AudioClip sonRessort;

    [Header("Affichage")]
    [Tooltip("Affiche une jauge de puissance pendant la charge.")]
    public bool afficherJauge = true;

    private Rigidbody rb;
    private Vector3 positionRepos;
    private float charge;              // 0 à 1
    private bool enCharge;
    private float finIgnorerCollision; // moment où le ressort recommence à toucher la bille
    private bool collisionIgnoree;

    private Pinball_input controls;
    private InputAction actionLancer;  // Espace + bouton sud de la manette

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        controls = new Pinball_input();
        actionLancer = new InputAction("Lancer", InputActionType.Button);
        actionLancer.AddBinding("<Keyboard>/space");
        actionLancer.AddBinding("<Gamepad>/buttonSouth");
    }

    void OnEnable()
    {
        controls.Enable();
        actionLancer.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
        actionLancer.Disable();
    }

    void OnDestroy()
    {
        controls?.Dispose();
        actionLancer?.Dispose();
    }

    void Start()
    {
        positionRepos = rb.position;

        if (bille == null)
        {
            GameObject go = null;
            try { go = GameObject.FindWithTag(global::Bille.Tag); } catch (UnityException) { }
            if (go == null) go = GameObject.Find("la_bille");
            if (go != null) bille = go.GetComponent<Rigidbody>();
        }
        if (zoneLanceur == null)
            zoneLanceur = FindAnyObjectByType<ZoneLanceur>();
        if (zoneLanceur == null)
            Debug.LogWarning("Script_ressort : aucune Zone Lanceur. Lancez Flipper > Installer.", this);
    }

    void Update()
    {
        bool appuiCetteFrame = controls.inputs.start.WasPressedThisFrame() || actionLancer.WasPressedThisFrame();
        bool maintenu = controls.inputs.start.IsPressed() || actionLancer.IsPressed();

        // Début de charge (pas pendant le GAME OVER, ni sur l'appui qui relance la partie)
        if (appuiCetteFrame && !enCharge && !EntreeBloquee())
        {
            enCharge = true;
            charge = 0f;
        }

        if (enCharge)
        {
            charge = Mathf.MoveTowards(charge, 1f, Time.deltaTime / Mathf.Max(0.01f, tempsChargeMax));
            if (!maintenu)
                Tirer();
        }
    }

    void FixedUpdate()
    {
        Vector3 cible = positionRepos - DirectionTir() * (distanceRecul * (enCharge ? charge : 0f));
        // Pendant la charge le ressort suit la jauge ; au relâchement il revient très vite.
        float vitesse = enCharge ? distanceRecul / Mathf.Max(0.01f, tempsChargeMax) * 2f : vitesseRetour;
        rb.MovePosition(Vector3.MoveTowards(rb.position, cible, vitesse * Time.fixedDeltaTime));

        if (collisionIgnoree && Time.time >= finIgnorerCollision)
            IgnorerCollisionBille(false);
    }

    void Tirer()
    {
        float vitesse = Mathf.Lerp(vitesseMin, vitesseMax, charge);
        enCharge = false;
        charge = 0f;

        if (audioSource != null && sonRessort != null)
            audioSource.PlayOneShot(sonRessort);

        if (bille == null || bille.isKinematic || !BilleDansCouloir())
            return;

        // Le ressort qui revient ne doit pas frapper la bille une 2e fois : tir identique à chaque fois
        IgnorerCollisionBille(true);
        finIgnorerCollision = Time.time + 0.3f;

        Vector3 direction = DirectionTir();
        // On retire la vitesse actuelle dans l'axe du tir, puis on donne l'impulsion (indépendante de la masse)
        bille.linearVelocity -= Vector3.Project(bille.linearVelocity, direction);
        bille.WakeUp();
        bille.AddForce(direction * vitesse, ForceMode.VelocityChange);
    }

    bool BilleDansCouloir()
    {
        if (zoneLanceur != null)
            return zoneLanceur.ContientBille();
        return Vector3.Distance(bille.position, rb.position) <= distanceDetectionSecours;
    }

    Vector3 DirectionTir()
    {
        if (zoneLanceur != null)
            return zoneLanceur.transform.forward;
        return directionTirSecours.sqrMagnitude > 0f ? directionTirSecours.normalized : Vector3.forward;
    }

    bool EntreeBloquee()
    {
        var gm = GameManager.Instance;
        return gm != null && (gm.PartieTerminee || gm.EnMenu || gm.FrameRejouer == Time.frameCount);
    }

    void IgnorerCollisionBille(bool ignorer)
    {
        collisionIgnoree = ignorer;
        if (bille == null) return;
        foreach (var cRessort in GetComponentsInChildren<Collider>())
            foreach (var cBille in bille.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(cRessort, cBille, ignorer);
    }

    void OnGUI()
    {
        if (!afficherJauge || !enCharge) return;

        // Taille proportionnelle à l'écran (même échelle que le score)
        float u = Screen.height / 1080f;
        float largeur = 160f * u, hauteur = 12f * u, marge = 14f * u;
        var cadre = new Rect(Screen.width - largeur - marge, Screen.height - hauteur - marge, largeur, hauteur);
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(cadre, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(Color.yellow, Color.red, charge);
        GUI.DrawTexture(new Rect(cadre.x + 2f, cadre.y + 2f, (largeur - 4f) * charge, hauteur - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        var styleJauge = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(10, Mathf.RoundToInt(18f * u)), alignment = TextAnchor.LowerRight };
        GUI.Label(new Rect(cadre.x, cadre.y - 26f * u, largeur, 24f * u), $"PUISSANCE {Mathf.RoundToInt(charge * 100f)} %", styleJauge);
    }
}
