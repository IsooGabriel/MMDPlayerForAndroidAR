using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class TapToPlace : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private Transform target; // 配置するモデルのルート

    private static readonly List<ARRaycastHit> hits = new();
    private bool isTouched = false;

    void Update()
    {
        if (!isTouched)
        {
            Place(arCamera.transform.forward);
        }

        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            return;
        }

        isTouched = true;

        var touch = touchscreen.primaryTouch;
        if (!touch.press.wasPressedThisFrame)
        {
            return;
        }

        Vector2 screenPos = touch.position.ReadValue();

        if (raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
        {
            Place(hits[0].pose.position);
        }
    }

    private void Place(Vector3 position)
    {
        target.position = position;

        // 水平方向にカメラの方を向かせる
        Vector3 toCamera = arCamera.transform.position - position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            target.rotation = Quaternion.LookRotation(toCamera);
        }

        target.gameObject.SetActive(true);
    }
}