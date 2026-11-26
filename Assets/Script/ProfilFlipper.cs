using UnityEngine;
using UnityEngine.SceneManagement;

// Ce qui change d'un flipper à l'autre dans le projet fusionné.
// Rien à mettre dans les scènes : tout se règle tout seul quand une scène se charge.
//   - Menu                : écran de choix (SelecteurFlipper)
//   - Flipper_Foret       : flipper du groupe  -> score, menu, high scores ajoutés au lancement (ScoreForet)
//   - Flipper_ForetBrulee : forêt en feu       -> comme avant
public static class ProfilFlipper
{
    public const string SceneMenu = "Menu";
    public const string SceneForet = "Flipper_Foret";
    public const string SceneForetBrulee = "Flipper_ForetBrulee";

    public static bool EstFlipper(string nomScene) => nomScene == SceneForet || nomScene == SceneForetBrulee;
    public static bool EnForet => SceneManager.GetActiveScene().name == SceneForet;

    /// <summary>Suffixe des sauvegardes (record, top 10). La forêt en feu garde ses anciennes sauvegardes.</summary>
    public static string Suffixe => EnForet ? SuffixeForet : SuffixeForetBrulee;
    public static string Titre => EnForet ? "FORÊT" : "FORÊT EN FEU";

    // Les auteurs de chaque flipper (affichés sur l'écran de choix et dans le menu du flipper)
    public const string AuteursForet = "Ethan Stasse · Rafael Hoff · Lana Schmitt";
    public const string AuteursForetBrulee = "Damien Rinaldis";
    public static string Auteurs => EnForet ? AuteursForet : AuteursForetBrulee;
    public const string SuffixeForet = "_Foret", SuffixeForetBrulee = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialiser()
    {
        SceneManager.sceneLoaded -= SurSceneChargee;
        SceneManager.sceneLoaded += SurSceneChargee;
    }

    // Filet de sécurité : avec "Enter Play Mode Options" (Reload Scene désactivé), Unity ne recharge pas
    // la scène en appuyant sur Play, donc sceneLoaded n'est jamais appelé. On prépare la scène ouverte ici.
    // (Sans danger si c'était déjà fait : tout vérifie d'abord si l'objet existe.)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void PreparerSceneOuverte()
    {
        SurSceneChargee(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void SurSceneChargee(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        // Chaque flipper garde le pas de physique avec lequel il a été réglé
        if (scene.name == SceneForet) Time.fixedDeltaTime = 0.02f;
        else if (scene.name == SceneForetBrulee) Time.fixedDeltaTime = 0.01f;
        Time.timeScale = 1f;

        if (scene.name == SceneMenu)
        {
            SelecteurFlipper.CreerSiAbsent();
            return;
        }
        if (!EstFlipper(scene.name)) return;

        var gm = GameManager.CreerSiAbsent();
        if (scene.name == SceneForet)
        {
            // Le flipper du groupe a déjà son propre anti-blocage et ses propres vitesses : on n'y touche pas
            gm.delaiBilleCoincee = 0f;
            gm.vitesseMaxBille = 1000f;
            ScoreForet.Brancher();
        }
        MenuFlipper.CreerSiAbsent();
    }
}
