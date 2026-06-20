using UnityEngine;

public class BillboardSprite : MonoBehaviour
{
    private Transform cam;

    private void Start() 
    {
        cam = Camera.main.transform;
    }

    private void LateUpdate() 
    {
        if (cam == null) return;

        Vector3 direction = cam.position - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.LookRotation(direction);
    }
}
