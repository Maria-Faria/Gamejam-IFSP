using UnityEngine;

public class MouseMovement : MonoBehaviour
{
  public float mouseSensitivity = 100f;

  float xRotation = 0f;
  float yRotation = 0f;

  public void Start()
  {
    Cursor.lockState = CursorLockMode.Locked;
  }

  public void Update()
  {
    float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
    float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

    xRotation = mouseY;
    xRotation = Mathf.Clamp(xRotation, -90f, 90f);

    yRotation = mouseX;

    transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
  }
}
