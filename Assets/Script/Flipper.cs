using UnityEngine;
using UnityEngine.InputSystem;

// Palette de flipper : tourne autour de l'axe Y local (la normale de la table)
// entre un angle de repos et un angle levé quand on appuie sur la touche.
// Le GameObject porte un Rigidbody cinématique : la palette (enfant avec collider)
// pousse la bille grâce à MoveRotation, ce qui transmet la vitesse de rotation.
[RequireComponent(typeof(Rigidbody))]
public class Flipper : MonoBehaviour
{
    public enum Cote { Gauche, Droite }

    [Header("Côté")]
    public Cote cote = Cote.Gauche;

    [Header("Angles (degrés, autour de l'axe Y local)")]
    public float angleRepos = 30f;
    public float angleLeve = -25f;

    [Header("Vitesses (degrés / seconde)")]
    public float vitesseMontee = 1800f;
    public float vitesseDescente = 600f;

    [Header("Commande supplémentaire (borne d'arcade, etc.)")]
    public InputAction actionSupplementaire;

    [Header("Son (optionnel)")]
    public AudioSource audioSource;
    public AudioClip sonFlipper;

    private Rigidbody rb;
    private Quaternion rotationBase;
    private float angleActuel;
    private bool etaitAppuye;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Dans la scène, le flipper est placé en position de repos :
        // on retire l'angle de repos pour retrouver l'orientation de la table.
        rotationBase = transform.rotation * Quaternion.Inverse(Quaternion.Euler(0f, angleRepos, 0f));
        angleActuel = angleRepos;
        rb.rotation = rotationBase * Quaternion.Euler(0f, angleActuel, 0f);
    }

    void OnEnable()
    {
        if (actionSupplementaire != null)
            actionSupplementaire.Enable();
    }

    void OnDisable()
    {
        if (actionSupplementaire != null)
            actionSupplementaire.Disable();
    }

    bool EstAppuye()
    {
        Keyboard clavier = Keyboard.current;
        if (clavier != null)
        {
            if (cote == Cote.Gauche &&
                (clavier.leftArrowKey.isPressed || clavier.leftShiftKey.isPressed || clavier.aKey.isPressed))
                return true;
            if (cote == Cote.Droite &&
                (clavier.rightArrowKey.isPressed || clavier.rightShiftKey.isPressed || clavier.dKey.isPressed))
                return true;
        }

        Gamepad manette = Gamepad.current;
        if (manette != null)
        {
            if (cote == Cote.Gauche && (manette.leftShoulder.isPressed || manette.leftTrigger.isPressed))
                return true;
            if (cote == Cote.Droite && (manette.rightShoulder.isPressed || manette.rightTrigger.isPressed))
                return true;
        }

        return actionSupplementaire != null && actionSupplementaire.enabled && actionSupplementaire.IsPressed();
    }

    void FixedUpdate()
    {
        bool appuye = EstAppuye();

        if (appuye && !etaitAppuye && audioSource != null && sonFlipper != null)
            audioSource.PlayOneShot(sonFlipper);
        etaitAppuye = appuye;

        float cible = appuye ? angleLeve : angleRepos;
        float vitesse = appuye ? vitesseMontee : vitesseDescente;

        angleActuel = Mathf.MoveTowards(angleActuel, cible, vitesse * Time.fixedDeltaTime);
        rb.MoveRotation(rotationBase * Quaternion.Euler(0f, angleActuel, 0f));
    }
}
