using TMPro;
using UnityEngine;

// Score affiché sur le backglass (la vitre du fronton), comme l'afficheur d'un vrai flipper.
// En partie : nom du joueur, gros score, bille et record. Au menu : titre et record. Fin : GAME OVER.
// Posé par le menu Flipper > Score sur le backglass.
public class ScoreBackglass : MonoBehaviour
{
    public TMP_Text haut;
    public TMP_Text score;
    public TMP_Text bas;
    public string titre = "FORÊT EN FEU";

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        string message = gm.MessageActuel;

        if (gm.EnMenu)
        {
            Ecrire(titre, $"{Mathf.Max(gm.Record, TableauScores.Meilleur):N0}", "RECORD");
            return;
        }
        if (gm.PartieTerminee)
        {
            Ecrire("GAME OVER", gm.Score.ToString("N0"), gm.NomJoueur);
            return;
        }
        string infos = $"BILLE {Mathf.Min(gm.BilleActuelle, gm.billesParPartie)}/{gm.billesParPartie}";
        if (gm.Multiplicateur > 1) infos += $"   x{gm.Multiplicateur}";
        if (gm.BillesEnPlus > 0) infos += "   2ND CHANCE";
        Ecrire(message ?? gm.NomJoueur, gm.Score.ToString("N0"), infos);
    }

    void Ecrire(string h, string s, string b)
    {
        if (haut != null && haut.text != h) haut.text = h;
        if (score != null && score.text != s) score.text = s;
        if (bas != null && bas.text != b) bas.text = b;
    }
}
