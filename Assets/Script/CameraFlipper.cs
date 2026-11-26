using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Deux vues de caméra : "joueur" (devant la machine, on voit la table et le backglass avec le score)
// et "dessus" (la vue d'origine, au-dessus de la table). Touche C (ou bouton nord de la manette) pour changer.
// Les deux vues sont enregistrées par le menu Flipper > Caméras (joueur / dessus).
//
// Cadrage automatique (au lancement et quand la fenêtre change de taille) : on garde l'angle de chaque vue
// et on place la caméra pour que la table (objet "Playfield") remplisse exactement l'écran, sans l'extérieur,
// en laissant libre la place du bandeau du score :
// le bandeau horizontal en haut de l'écran (dans les deux vues) : la table s'arrête juste en dessous.
[RequireComponent(typeof(Camera))]
public class CameraFlipper : MonoBehaviour
{
    [System.Serializable]
    public struct Vue
    {
        public Vector3 position;
        public Quaternion rotation;
        public float champ;   // field of view
    }

    public Vue vueJoueur;
    public Vue vueDessus;
    [Tooltip("Vue au lancement : true = joueur, false = dessus.")]
    public bool commencerEnVueJoueur = true;
    [Tooltip("Durée du passage d'une vue à l'autre (secondes).")]
    public float dureeTransition = 0.6f;

    [Header("Cadrage automatique")]
    [Tooltip("Place la caméra pour cadrer exactement la table, en laissant la place du bandeau du score.")]
    public bool cadrageAuto = true;
    [Tooltip("Petite marge autour de la table (fraction de l'écran).")]
    [Range(0f, 0.1f)] public float marge = 0.015f;

    /// <summary>La caméra est-elle en vue joueur (backglass visible) ?</summary>
    public static bool VueJoueur { get; private set; }

    Camera cam;
    float t;   // 0 = dessus, 1 = joueur
    Vue joueurOrigine, dessusOrigine;
    int largeurEcran, hauteurEcran;

    void Awake()
    {
        cam = GetComponent<Camera>();
        joueurOrigine = vueJoueur;
        dessusOrigine = vueDessus;
        if (cadrageAuto) Cadrer();
        VueJoueur = commencerEnVueJoueur;
        t = VueJoueur ? 1f : 0f;
        Appliquer(t);
    }

    void Update()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        if ((k != null && k.cKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame))
            VueJoueur = !VueJoueur;

        // La fenêtre a changé de taille (plein écran, Game view...) : on recadre
        bool recadre = cadrageAuto && (Screen.width != largeurEcran || Screen.height != hauteurEcran);
        if (recadre) Cadrer();

        float cible = VueJoueur ? 1f : 0f;
        if (Mathf.Approximately(t, cible) && !recadre) return;
        t = Mathf.MoveTowards(t, cible, Time.unscaledDeltaTime / Mathf.Max(0.01f, dureeTransition));
        Appliquer(Mathf.SmoothStep(0f, 1f, t));
    }

    void Appliquer(float k)
    {
        transform.SetPositionAndRotation(Vector3.Lerp(vueDessus.position, vueJoueur.position, k),
                                         Quaternion.Slerp(vueDessus.rotation, vueJoueur.rotation, k));
        cam.fieldOfView = Mathf.Lerp(vueDessus.champ, vueJoueur.champ, k);
    }

    // ==================================================================== CADRAGE

    void Cadrer()
    {
        largeurEcran = Screen.width;
        hauteurEcran = Screen.height;
        GameObject plateau = GameObject.Find("Playfield");
        if (plateau == null || largeurEcran <= 0 || hauteurEcran <= 0) return;

        var table = new List<Vector3>();
        AjouterCoins(plateau, table);

        // Épaisseur du bandeau du score en pixels (même calcul que le CanvasScaler du menu : 1920 x 1080, match 0.5)
        float echelle = Mathf.Sqrt(largeurEcran / 1920f * (hauteurEcran / 1080f));
        float bandeau = MenuFlipper.HauteurHud * echelle;

        // Vue dessus : uniquement la table, bandeau horizontal en haut
        vueDessus = Cadrage(dessusOrigine, table, 0f, bandeau / hauteurEcran);

        // Vue joueur : la table et le backglass (avec son score), bandeau horizontal en haut
        var avecBackglass = new List<Vector3>(table);
        GameObject backglass = GameObject.Find("Backglass");
        if (backglass != null) AjouterCoins(backglass, avecBackglass);
        vueJoueur = Cadrage(joueurOrigine, avecBackglass, 0f, bandeau / hauteurEcran);
    }

    // Les 8 coins de l'objet (boîte de son maillage, tournée avec lui : colle à la table penchée)
    static void AjouterCoins(GameObject go, List<Vector3> points)
    {
        var mf = go.GetComponent<MeshFilter>();
        Bounds b;
        Matrix4x4 m;
        if (mf != null && mf.sharedMesh != null) { b = mf.sharedMesh.bounds; m = go.transform.localToWorldMatrix; }
        else if (go.TryGetComponent(out Renderer r)) { b = r.bounds; m = Matrix4x4.identity; }
        else return;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1);
            points.Add(m.MultiplyPoint3x4(b.min + Vector3.Scale(b.size, c)));
        }
    }

    // Garde l'angle et le champ de la vue, et calcule la position la plus proche où tous les points sont visibles.
    // reserveDroite / reserveHaut : fraction de l'écran laissée libre à droite / en haut (pour le bandeau).
    Vue Cadrage(Vue v, List<Vector3> points, float reserveDroite, float reserveHaut)
    {
        float tV = Mathf.Tan(v.champ * 0.5f * Mathf.Deg2Rad);
        float tH = tV * largeurEcran / hauteurEcran;
        float m = 2f * marge;
        // Limites de l'image (en tangente) : gauche, droite, bas, haut
        float tG = tH * (1f - m), tD = tH * (1f - 2f * reserveDroite - m);
        float tB = tV * (1f - m), tHt = tV * (1f - 2f * reserveHaut - m);

        // Dans le repère de la caméra (x droite, y haut, z avant), caméra en (cx, cy, -u) :
        //   x - cx <= tD (z + u)   et   cx - x <= tG (z + u)   (pareil en y)
        // -> contraintes linéaires : u minimum puis cx, cy au milieu de ce qui est permis.
        Quaternion inv = Quaternion.Inverse(v.rotation);
        float ax = float.MinValue, bx = float.MaxValue, ay = float.MinValue, by = float.MaxValue;
        foreach (Vector3 p in points)
        {
            Vector3 q = inv * p;
            ax = Mathf.Max(ax, q.x - tD * q.z);
            bx = Mathf.Min(bx, q.x + tG * q.z);
            ay = Mathf.Max(ay, q.y - tHt * q.z);
            by = Mathf.Min(by, q.y + tB * q.z);
        }
        float u = Mathf.Max((ax - bx) / (tG + tD), (ay - by) / (tB + tHt));
        float cx = (ax - tD * u + bx + tG * u) * 0.5f;
        float cy = (ay - tHt * u + by + tB * u) * 0.5f;
        return new Vue { position = v.rotation * new Vector3(cx, cy, -u), rotation = v.rotation, champ = v.champ };
    }
}
