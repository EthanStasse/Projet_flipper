using UnityEngine;

// Arbre en feu : la lumière du feu vacille, et quand la bille tape l'arbre les flammes jaillissent.
// La poussée de la bille et les points sont gérés par BumperEffect (sur le même objet).
// Le prefab est créé par le menu Flipper > Ajouter des arbres en feu.
public class ArbreEnFeu : MonoBehaviour
{
    [Tooltip("Flammes (particules). Laisser vide = trouvées automatiquement.")]
    public ParticleSystem flammes;
    [Tooltip("Lueur du feu. Laisser vide = trouvée automatiquement.")]
    public Light lumiere;

    [Header("Lueur")]
    public float intensiteBase = 2f;
    [Range(0f, 1f)] public float variation = 0.6f;
    public float vitesseVacillement = 8f;

    [Header("Au choc")]
    [Tooltip("Nombre de flammes supplémentaires quand la bille tape l'arbre.")]
    public int flammesAuChoc = 40;
    [Tooltip("Lumière en plus au moment du choc.")]
    public float boostLumiere = 4f;

    private float boost;
    private float graine;

    void Awake()
    {
        graine = Random.value * 100f;   // chaque arbre vacille différemment
        if (flammes == null) flammes = GetComponentInChildren<ParticleSystem>();
        if (lumiere == null) lumiere = GetComponentInChildren<Light>();
    }

    void Update()
    {
        if (lumiere == null) return;
        boost = Mathf.MoveTowards(boost, 0f, Time.deltaTime * boostLumiere * 3f);
        float bruit = Mathf.PerlinNoise(graine, Time.time * vitesseVacillement);
        lumiere.intensity = intensiteBase * (1f - variation * 0.5f + variation * bruit) + boost;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!Bille.Est(collision.gameObject)) return;
        if (flammes != null) flammes.Emit(flammesAuChoc);
        boost = boostLumiere;
    }
}
