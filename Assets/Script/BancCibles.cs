using System.Collections;
using UnityEngine;

// Banc de cibles tombantes : quand toutes les cibles sont tombées, bonus, puis elles remontent.
// Remontent aussi à chaque nouvelle partie. Créé par le menu Flipper > Haut du plateau.
public class BancCibles : MonoBehaviour
{
    public CibleTombante[] cibles;
    [Tooltip("Points quand toutes les cibles sont tombées.")]
    public int bonus = 2500;
    public string message = "CIBLES !";
    [Tooltip("Attente avant que les cibles remontent (secondes).")]
    public float delaiRemontee = 1f;
    public AudioClip sonBonus;
    [Range(0f, 1f)] public float volume = 0.8f;

    /// <summary>Toutes les cibles d'un banc viennent de tomber.</summary>
    public static event System.Action BancComplet;

    GameManager abonne;
    bool remonteeEnCours;

    void Start()
    {
        foreach (CibleTombante c in cibles)
            if (c != null) c.banc = this;
        abonne = GameManager.Instance;
        if (abonne != null) abonne.SurNouvellePartie += ToutRelever;
    }

    void OnDestroy()
    {
        if (abonne != null) abonne.SurNouvellePartie -= ToutRelever;
    }

    public void CibleTombee(CibleTombante cible)
    {
        foreach (CibleTombante c in cibles)
            if (c != null && !c.Tombee) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AjouterPoints(bonus);
            GameManager.Instance.AfficherMessage($"{message}  +{bonus}");
        }
        Sons.Jouer(this, sonBonus, volume, 0f);
        BancComplet?.Invoke();
        if (!remonteeEnCours) StartCoroutine(Remonter());
    }

    IEnumerator Remonter()
    {
        remonteeEnCours = true;
        yield return new WaitForSeconds(delaiRemontee);
        // On attend que la bille ne soit plus au-dessus d'une cible
        while (System.Array.Exists(cibles, c => c != null && c.BilleAuDessus()))
            yield return new WaitForSeconds(0.1f);
        foreach (CibleTombante c in cibles)
            if (c != null) c.Relever();
        remonteeEnCours = false;
    }

    void ToutRelever()
    {
        StopAllCoroutines();
        remonteeEnCours = false;
        foreach (CibleTombante c in cibles)
            if (c != null) c.Relever();
    }
}
