using System.Net.NetworkInformation;
using UnityEngine;

public class FlipActive : MonoBehaviour
{
    [SerializeField]
    private GameObject logObject;

    public void OnFlipActive()
    {
        logObject.SetActive(!logObject.activeSelf);
    }
}
