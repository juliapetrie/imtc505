using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    public float amplitude = 0.1f;   // how far it moves up and down
    public float frequency = 1f;     // how fast it bobs
    public float rotationSpeed = 20f; // degrees per second, set to 0 for no spin

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float yOffset = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) * amplitude;
        transform.position = startPos + new Vector3(0f, yOffset, 0f);
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }
}