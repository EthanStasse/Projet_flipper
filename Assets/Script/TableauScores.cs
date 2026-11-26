using System;
using System.Collections.Generic;
using UnityEngine;

// Les 10 meilleurs scores (nom + score + date), enregistrés sur l'ordinateur (PlayerPrefs, en JSON).
public static class TableauScores
{
    public const int Taille = 10;
    // Un top 10 par flipper : "HighScores" (Forêt en feu), "HighScores_Foret" (Forêt)
    static string Cle => "HighScores" + ProfilFlipper.Suffixe;
    static string cleCache;

    [Serializable]
    public class Ligne
    {
        public string nom;
        public int score;
        public string date;
    }

    [Serializable]
    class Liste { public List<Ligne> lignes = new List<Ligne>(); }

    static Liste cache;

    /// <summary>Les scores, du meilleur au moins bon (au plus 10).</summary>
    public static IReadOnlyList<Ligne> Lignes => Charger().lignes;

    public static int Meilleur => Charger().lignes.Count > 0 ? Charger().lignes[0].score : 0;

    /// <summary>Le top 10 d'un flipper précis (pour l'écran de choix) : suffixe "" = Forêt en feu, "_Foret" = Forêt.</summary>
    public static List<Ligne> LignesDe(string suffixe)
    {
        string json = PlayerPrefs.GetString("HighScores" + suffixe, "");
        Liste l = null;
        try { if (!string.IsNullOrEmpty(json)) l = JsonUtility.FromJson<Liste>(json); }
        catch (ArgumentException) { }
        return l?.lignes ?? new List<Ligne>();
    }

    /// <summary>Le score entrerait-il dans le top 10 ?</summary>
    public static bool EntreDansLeTop(int score)
    {
        var l = Charger().lignes;
        return score > 0 && (l.Count < Taille || score > l[l.Count - 1].score);
    }

    /// <summary>Ajoute le score s'il entre dans le top 10. Renvoie son rang (1 = premier) ou 0 s'il n'y entre pas.</summary>
    public static int Ajouter(string nom, int score)
    {
        if (!EntreDansLeTop(score)) return 0;
        var l = Charger().lignes;
        var ligne = new Ligne { nom = nom, score = score, date = DateTime.Now.ToString("dd/MM/yyyy") };
        // À score égal, le plus ancien garde sa place
        int i = 0;
        while (i < l.Count && l[i].score >= score) i++;
        l.Insert(i, ligne);
        if (l.Count > Taille) l.RemoveRange(Taille, l.Count - Taille);
        Enregistrer();
        return i + 1;
    }

    public static void ToutEffacer()
    {
        Charger().lignes.Clear();
        Enregistrer();
    }

    static Liste Charger()
    {
        if (cache != null && cleCache == Cle) return cache;
        cleCache = Cle;
        string json = PlayerPrefs.GetString(Cle, "");
        try { cache = string.IsNullOrEmpty(json) ? new Liste() : JsonUtility.FromJson<Liste>(json); }
        catch (ArgumentException) { cache = null; }
        if (cache == null || cache.lignes == null) cache = new Liste();
        return cache;
    }

    static void Enregistrer()
    {
        PlayerPrefs.SetString(Cle, JsonUtility.ToJson(Charger()));
        PlayerPrefs.Save();
    }
}
