using UnityEngine;

[CreateAssetMenu(menuName = "Pmx/Material Templates")]
public sealed class PmxMaterialTemplates : ScriptableObject
{
    public Material opaque;         // URP Lit, Alpha Clipping OFF
    public Material cutout;         // URP Lit, Alpha Clipping ON
    public Material transparent;    // URP Lit, Surface Type = Transparent
}