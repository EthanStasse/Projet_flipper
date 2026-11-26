using System.Collections;
using UnityEngine;

// Échelle de score : 5 étoiles au centre bas du plateau (100, 200, 300, 500, 1000).
// - À chaque déclencheur (par défaut : la bille entre dans la rampe), l'étoile suivante s'allume
//   et donne ses points tout de suite.
// - Quand la dernière (1000) s'allume : power-up "points x2" pendant 30 s, toutes les étoiles
//   clignotent 2 s, puis l'échelle repart de zéro.
// - Remise à zéro à la perte de la bille (sauf si "garderProgressionSiBillePerdue").
// D'autres scripts peuvent faire avancer l'échelle avec Avancer().
// Créée par le menu Flipper > Échelle d'étoiles.
public class EchelleScore : MonoBehaviour
{
    public enum Declencheur { Rampe, Bumper, Cible, Aucun }

    [Header("Échelle")]
    [Tooltip("Points donnés par chaque étoile, de la première (en bas) à la dernière (en haut).")]
    public int[] valeurs = { 100, 200, 300, 500, 1000 };
    [Tooltip("Les étoiles, dans le même ordre que les valeurs.")]
    public EtoileEchelle[] etoiles;
    [Tooltip("Ce qui allume l'étoile suivante. \"Aucun\" : seulement par Avancer() depuis un autre script.")]
    public Declencheur declencheur = Declencheur.Rampe;
    [Tooltip("Garder les étoiles allumées quand la bille est perdue.")]
    public bool garderProgressionSiBillePerdue = false;

    [Header("Power-up (dernière étoile)")]
    public int multiplicateurBonus = 2;
    [Tooltip("Durée du multiplicateur de points (secondes).")]
    public float dureeBonus = 30f;
    [Tooltip("Durée du clignotement de toutes les étoiles avant la remise à zéro (secondes).")]
    public float dureeClignotement = 2f;

    [Header("Sons")]
    public AudioClip sonEtoile;
    public AudioClip sonComplete;
    [Range(0f, 1f)] public float volume = 0.8f;

    public int Niveau { get; private set; }
    public bool BonusActif { get; private set; }

    bool enClignotement;
    Coroutine coroutineBonus, coroutineClignotement;
    GameManager abonne;

    void OnEnable()
    {
        BoostRail.EntreeRail += SurEntreeRail;
        BumperEffectBrule.Touche += SurBumper;
        CibleBonus.Touchee += SurCible;
    }

    void OnDisable()
    {
        BoostRail.EntreeRail -= SurEntreeRail;
        BumperEffectBrule.Touche -= SurBumper;
        CibleBonus.Touchee -= SurCible;
        if (abonne != null)
        {
            abonne.SurBillePerdue -= SurBillePerdue;
            abonne.SurNouvellePartie -= SurNouvellePartie;
            abonne = null;
        }
    }

    void Start()
    {
        foreach (EtoileEchelle e in etoiles)
            if (e != null) e.Eteindre();
    }

    void Update()
    {
        // Le GameManager est créé au chargement de la scène : on s'y abonne dès qu'il existe
        if (abonne == null && GameManager.Instance != null)
        {
            abonne = GameManager.Instance;
            abonne.SurBillePerdue += SurBillePerdue;
            abonne.SurNouvellePartie += SurNouvellePartie;
        }
    }

    void SurEntreeRail() { if (declencheur == Declencheur.Rampe) Avancer(); }
    void SurBumper()     { if (declencheur == Declencheur.Bumper) Avancer(); }
    void SurCible()      { if (declencheur == Declencheur.Cible) Avancer(); }

    /// <summary>Allume l'étoile suivante et donne ses points. Sans effet pendant le clignotement final.</summary>
    public void Avancer()
    {
        if (enClignotement || etoiles == null || Niveau >= etoiles.Length) return;

        EtoileEchelle etoile = etoiles[Niveau];
        if (etoile != null) etoile.Allumer(true);
        int points = Niveau < valeurs.Length ? valeurs[Niveau] : 0;
        Niveau++;

        bool complete = Niveau >= etoiles.Length;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AjouterPoints(points);
            if (!complete) GameManager.Instance.AfficherMessage($"ÉTOILE {points}  +{points}");
        }
        Sons.Jouer(this, complete ? sonComplete : sonEtoile, volume, 0.02f);

        if (complete)
        {
            if (coroutineBonus != null) StopCoroutine(coroutineBonus);
            coroutineBonus = StartCoroutine(Bonus());
            coroutineClignotement = StartCoroutine(ClignoterPuisRemettreAZero());
        }
    }

    /// <summary>Éteint toutes les étoiles et repart de zéro (le bonus en cours continue).</summary>
    public void Reinitialiser()
    {
        if (coroutineClignotement != null) StopCoroutine(coroutineClignotement);
        coroutineClignotement = null;
        enClignotement = false;
        Niveau = 0;
        foreach (EtoileEchelle e in etoiles)
            if (e != null) e.Eteindre();
    }

    IEnumerator Bonus()
    {
        BonusActif = true;
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.Multiplicateur = multiplicateurBonus;
            gm.AfficherMessage($"ÉCHELLE COMPLÈTE !  POINTS x{multiplicateurBonus} PENDANT {dureeBonus:0} s", 2.5f);
        }
        yield return new WaitForSeconds(dureeBonus);
        if (gm != null)
        {
            gm.Multiplicateur = 1;
            gm.AfficherMessage($"FIN DU x{multiplicateurBonus}");
        }
        BonusActif = false;
        coroutineBonus = null;
    }

    IEnumerator ClignoterPuisRemettreAZero()
    {
        enClignotement = true;
        foreach (EtoileEchelle e in etoiles)
            if (e != null) e.Clignoter(true);
        yield return new WaitForSeconds(dureeClignotement);
        coroutineClignotement = null;
        Reinitialiser();
    }

    void SurBillePerdue()
    {
        if (!garderProgressionSiBillePerdue) Reinitialiser();
    }

    void SurNouvellePartie()
    {
        // Le GameManager remet déjà le multiplicateur à 1
        if (coroutineBonus != null) StopCoroutine(coroutineBonus);
        coroutineBonus = null;
        BonusActif = false;
        Reinitialiser();
    }
}
