// Fly camera: WASD to move, mouse to look, Shift to boost, Space/Ctrl for
// vertical. CreateMesh follows Camera.main to stream chunks.

using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMotion : MonoBehaviour {

	public float moveSpeed = 80f;      // units per second
	public float boostSpeed = 250f;    // units per second while holding Shift
	public float lookSensitivity = 1f; // multiplier on mouse look speed

	float pitch = 0f;

	void Start () {
		Cursor.visible = false;
		Cursor.lockState = CursorLockMode.Locked;
		pitch = transform.eulerAngles.x;
		if (pitch > 180f) pitch -= 360f; // normalize to [-180, 180)
	}

	// move the camera based on input
	void Update () {
		var mouse = Mouse.current;
		var keyboard = Keyboard.current;
		if (mouse == null || keyboard == null) return;

		// release the cursor with Escape
		if (keyboard.escapeKey.wasPressedThisFrame)
		{
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
			return;
		}

		// mouse look
		Vector2 md = mouse.delta.ReadValue() * 0.5f * lookSensitivity;
		pitch = Mathf.Clamp(pitch - md.y, -90f, 90f);
		Vector3 rotation = transform.eulerAngles;
		rotation.y += md.x;
		rotation.x = pitch;
		transform.eulerAngles = rotation;

		// WASD movement, Shift to boost, Space/Ctrl for vertical
		float speed = keyboard.leftShiftKey.isPressed ? boostSpeed : moveSpeed;

		float dx = 0f, dy = 0f, dz = 0f;
		if (keyboard.aKey.isPressed) dx -= 1f;
		if (keyboard.dKey.isPressed) dx += 1f;
		if (keyboard.wKey.isPressed) dz += 1f;
		if (keyboard.sKey.isPressed) dz -= 1f;
		if (keyboard.spaceKey.isPressed) dy += 1f;
		if (keyboard.leftCtrlKey.isPressed) dy -= 1f;

		transform.Translate(new Vector3(dx, dy, dz) * (speed * Time.deltaTime), Space.Self);
	}
}
