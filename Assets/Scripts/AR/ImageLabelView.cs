using MMDPlayerForVR.PmxImporter;
using TMPro;
using UnityEngine;
using VContainer;
using System.IO;

public class ImageLabelView : MonoBehaviour
{
    public TextMeshProUGUI tmpro;
    public string labelHead = "MMD4AR_GGNM_";
    private PmxRuntimeLoader _pmxRuntimeLoader;

    [Inject]
    public void Inject(PmxRuntimeLoader pmxRuntimeLoader)
    {
        _pmxRuntimeLoader = pmxRuntimeLoader;
        _pmxRuntimeLoader.OnFinishInitiation += ChangeLabel;
        Debug.Log("imageLabelView.Inject");
    }

    public void ChangeLabel(string content)
    {
        if (_pmxRuntimeLoader == null)
        {
            return;
        }
        string label = labelHead;
        label += content;
        ChangeLabelAbsolute(label);
    }

    public void ChangeLabelAbsolute(string label)
    {
        tmpro.text = label;
    }
    
}
