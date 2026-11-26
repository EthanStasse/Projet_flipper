using UnityEngine;
using UnityEngine.InputSystem;

// Flipper (batte) piloté par un Rigidbody cinématique.
// Le pivot du flipper est l'origine de CE GameObject : le visuel + collider est un enfant décalé.
[RequireComponent(typeof(Rigidbody))]
public class FlipperBrule : MonoBehaviour
{
    [Header("Côté")]
    [Tooltip("Coché = flipper gauche, décoché = flipper droit (inverse le sens de rotation).")]
    public bool estGauche = true;

    [Header("Mouvement")]
    [Tooltip("Angle de levée en degrés (depuis la position de repos).")]
    public float angleMax = 50f;
    [Tooltip("Vitesse de levée en degrés/seconde. 900 ≈ bille à 15 m/s (atteint le haut de la table). Trop haut = la bille s'envole.")]
    public float vitesseMontee = 900f;
    [Tooltip("Vitesse de retour en degrés/seconde.")]
    public float vitesseDescente = 900f;

    [Header("Commande")]
    [Tooltip("Laisser vide pour les touches par défaut (Shift / flèche / A-D / gâchettes de manette). " +
             "Vous pouvez ajouter ici le bouton de votre borne d'arcade.")]
    public InputAction bouton;

    [Header("Son (optionnel)")]
    public AudioSource audioSource;
    public AudioClip sonFlipper;

    private Rigidbody rb;
    private Quaternion rotationRepos;
    private Vector3 axe;
    private float angleActuel;
    private bool appuye;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        rotationRepos = transform.rotation;
        axe = transform.up; // normale de la table

        if (bouton == null || bouton.bindings.Count == 0)
        {
            bouton = new InputAction(estGauche ? "FlipperGauche" : "FlipperDroit", InputActionType.Button);
            if (estGauche)
            {
                // Flèches + Q (et Shift). "#(q)" = la touche où est écrit Q, quel que soit le clavier
                // (AZERTY français ou QWERTY) ; "<Keyboard>/q" serait la position du Q américain (= A en AZERTY).
                bouton.AddBinding("<Keyboard>/leftShift");
                bouton.AddBinding("<Keyboard>/leftArrow");
                bouton.AddBinding("<Keyboard>/#(q)");
                bouton.AddBinding("<Gamepad>/leftShoulder");
                bouton.AddBinding("<Gamepad>/leftTrigger");
            }
            else
            {
                bouton.AddBinding("<Keyboard>/rightShift");
                bouton.AddBinding("<Keyboard>/rightArrow");
                bouton.AddBinding("<Keyboard>/#(d)");
                bouton.AddBinding("<Gamepad>/rightShoulder");
                bouton.AddBinding("<Gamepad>/rightTrigger");
            }
        }
    }

    void OnEnable()  { bouton?.Enable(); }
    void OnDisable() { bouton?.Disable(); }

    void Update()
    {
        bool presse = bouton != null && bouton.IsPressed();

        if (presse && !appuye && audioSource != null && sonFlipper != null)
            audioSource.PlayOneShot(sonFlipper);

        appuye = presse;
    }

    void FixedUpdate()
    {
        float cible = appuye ? angleMax : 0f;
        float vitesse = appuye ? vitesseMontee : vitesseDescente;
        angleActuel = Mathf.MoveTowards(angleActuel, cible, vitesse * Time.fixedDeltaTime);

        float signe = estGauche ? -1f : 1f;
        rb.MoveRotation(Quaternion.AngleAxis(signe * angleActuel, axe) * rotationRepos);
    }
}
