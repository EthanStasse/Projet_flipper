using UnityEngine;
using UnityEngine.InputSystem;

public class AllerRetour : MonoBehaviour
{
    public float distance = 2f;
    public float vitesseAller = 3f;
    public float variationVitesse = 3f; // marge aléatoire +/- autour de vitesseAller
    public float vitesseRetour = 1f;
    public Vector3 direction = Vector3.forward;

    private Rigidbody rb;
    private Vector3 positionDepart;
    private Vector3 positionArrivee;
    private bool enMouvement = false;
    private bool versArrivee = true;
    private float vitesseAllerActuelle;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        positionDepart = transform.position;

        Vector3 directionMonde = transform.TransformDirection(direction.normalized);
        positionArrivee = positionDepart + directionMonde * distance;
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !enMouvement)
        {
            enMouvement = true;
            versArrivee = true;

            // Nouvelle vitesse aléatoire à chaque déclenchement
            vitesseAllerActuelle = vitesseAller + Random.Range(-variationVitesse, variationVitesse);
            vitesseAllerActuelle = Mathf.Max(0.1f, vitesseAllerActuelle); // évite une vitesse négative ou nulle
        }
    }

    void FixedUpdate()
    {
        if (enMouvement)
        {
            Vector3 cible = versArrivee ? positionArrivee : positionDepart;
            float vitesse = versArrivee ? vitesseAllerActuelle : vitesseRetour;

            Vector3 nouvellePosition = Vector3.MoveTowards(rb.position, cible, vitesse * Time.fixedDeltaTime);
            rb.MovePosition(nouvellePosition);

            if (Vector3.Distance(nouvellePosition, cible) < 0.01f)
            {
                if (versArrivee)
                    versArrivee = false;
                else
                    enMouvement = false;
            }
        }
    }
}