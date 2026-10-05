
using UnityEngine;

namespace  MMDPlayerForVR
{
    public class Scaler:MonoBehaviour
    {
        [SerializeField]
        private GameObject model;

        public void OnChangeScale(float value)
        {
            model.transform.localScale = Vector3.one*value;
        }
    }
}
