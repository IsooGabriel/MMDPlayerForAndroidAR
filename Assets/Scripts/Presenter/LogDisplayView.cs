using System.Net.NetworkInformation;
using UnityEngine;

public class LogDisplayView : MonoBehaviour
{
    [SerializeField]
    private GameObject logObject;

    public void OnClickDisplayButton()
    {
        logObject.SetActive(!logObject.activeSelf);
    }
}
