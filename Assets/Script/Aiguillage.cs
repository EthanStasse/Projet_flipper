using UnityEngine;

// Aiguillage de rail : envoie la bille alternativement dans le couloir de gauche puis dans celui de droite.
// La lame ne bloque pas la bille (son collider est une zone "Is Trigger") : tant que la bille la traverse
// en montant, elle est poussée sur le côté choisi (dès que le couloir s'élargit, elle se décale et passe
// du bon côté de la pointe). La lame pivote pour montrer le côté choisi.
// À mettre sur la lame. Flipper > Installer le fait pour l'objet "Aiguillage".
[RequireComponent(typeof(Rigidbody))]
public class Aiguillage : MonoBehaviour
{
    [Tooltip("Coché = la prochaine bille part à gauche.")]
    public bool versLaGauche = true;
    [Tooltip("Vitesse sur le côté (m/s) maintenue pendant que la bille traverse la lame.")]
    public float pousseeLaterale = 6f;

    [Header("Animation de la lame")]
    [Tooltip("Angle (degrés) de la lame de chaque côté.")]
    public float angle = 12f;
    [Tooltip("Vitesse de pivot de la lame (degrés/seconde).")]
    public float vitessePivot = 400f;

    private Rigidbody rb;
    private Quaternion rotationMilieu;
    private Vector3 axe, cote, avant;
    private float angleActuel;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        foreach (var c in GetComponents<Collider>()) c.isTrigger = true;

        rotationMilieu = transform.rotation;
        axe = transform.up;        // normale de la table
        cote = transform.right;    // vers la droite de la table
        avant = transform.forward; // vers le haut de la table
        angleActuel = AngleCible();
        transform.rotation = Quaternion.AngleAxis(angleActuel, axe) * rotationMilieu;
    }

    // La pointe de la lame (en bas) part du côté opposé à celui où on envoie la bille
    float AngleCible() => versLaGauche ? -angle : angle;

    void FixedUpdate()
    {
        angleActuel = Mathf.MoveTowards(angleActuel, AngleCible(), vitessePivot * Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.AngleAxis(angleActuel, axe) * rotationMilieu);
    }

    void OnTriggerStay(Collider other)
    {
        Rigidbody bille = other.attachedRigidbody;
        if (bille == null || !Bille.Est(bille.gameObject)) return;

        Vector3 v = bille.linearVelocity;
        if (Vector3.Dot(v, avant) <= 0f) return;   // la bille redescend : on ne fait rien

        // Vitesse sur le côté choisi au moins égale à pousseeLaterale
        Vector3 direction = versLaGauche ? -cote : cote;
        float laterale = Vector3.Dot(v, direction);
        if (laterale < pousseeLaterale)
            bille.linearVelocity = v + direction * (pousseeLaterale - laterale);
    }

    void OnTriggerExit(Collider other)
    {
        Rigidbody bille = other.attachedRigidbody;
        if (bille != null && Bille.Est(bille.gameObject))
            versLaGauche = !versLaGauche;   // la bille suivante partira de l'autre côté
    }
}
