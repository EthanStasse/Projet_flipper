using System.Collections;
using UnityEngine;

// Groupe de pastilles de bonus (CibleBonus en enfants).
// Quand toutes les pastilles sont allumées : gros bonus, clignotement, puis tout se rééteint.
public class GroupeBonus : MonoBehaviour
{
    [Tooltip("Points donnés quand toutes les pastilles du groupe sont allumées.")]
    public int bonusComplet = 5000;
    [Tooltip("Le bonus augmente à chaque fois que le groupe est complété dans la même partie (x1, x2, x3...).")]
    public bool multiplicateur = true;
    public string message = "BONUS";

    [Header("Son")]
    public AudioClip son;
    [Range(0f, 1f)] public float volume = 0.9f;

    [Header("Clignotement")]
    public int nbClignotements = 6;
    public float dureeClignotement = 0.12f;

    private CibleBonus[] cibles;
    private int nbCompletes;
    private bool enAnimation;

    void Awake()
    {
        cibles = GetComponentsInChildren<CibleBonus>();
    }

    public void CibleAllumee()
    {
        if (enAnimation) return;
        foreach (var c in cibles)
            if (!c.Allumee) return;

        nbCompletes++;
        int bonus = multiplicateur ? bonusComplet * nbCompletes : bonusComplet;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AjouterPoints(bonus);
            GameManager.Instance.AfficherMessage(multiplicateur && nbCompletes > 1
                ? $"{message} x{nbCompletes}  +{bonus}"
                : $"{message}  +{bonus}");
        }
        Sons.Jouer(this, son, volume, 0f);
        StartCoroutine(Clignoter());
    }

    // Appelé par le GameManager au début d'une nouvelle partie
    public void Reinitialiser()
    {
        StopAllCoroutines();
        enAnimation = false;
        nbCompletes = 0;
        foreach (var c in cibles) c.Allumer(false);
    }

    IEnumerator Clignoter()
    {
        enAnimation = true;
        for (int i = 0; i < nbClignotements; i++)
        {
            foreach (var c in cibles) c.Appliquer(i % 2 == 0);
            yield return new WaitForSeconds(dureeClignotement);
        }
        foreach (var c in cibles) c.Allumer(false);
        enAnimation = false;
    }
}
