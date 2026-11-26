using UnityEngine;

// Joue un son "2D" (même volume où que soit l'objet sur la table) avec une petite variation de hauteur,
// pour que le même son répété ne soit pas monotone. Ajoute un AudioSource à l'objet si besoin.
public static class Sons
{
    public static void Jouer(Component objet, AudioClip son, float volume = 1f, float variationHauteur = 0.06f)
    {
        if (son == null || objet == null) return;

        AudioSource source = objet.GetComponent<AudioSource>();
        if (source == null)
        {
            source = objet.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // 2D
        }
        source.pitch = 1f + Random.Range(-variationHauteur, variationHauteur);
        source.PlayOneShot(son, volume);
    }
}
