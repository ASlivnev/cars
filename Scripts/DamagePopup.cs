using UnityEngine;

// Всплывающая цифра урона. НЕ вешать этот компонент руками ни на что (в том числе на машину) -
// он создаётся сам, только через статический метод DamagePopup.Show(...), который сам строит
// себе отдельный GameObject с 3D-текстом (TextMesh, без Canvas/UI - надёжнее и проще), взлетает
// вверх, плавно растворяется и уничтожает себя. CarHealth уже вызывает Show() сам при уроне.
public class DamagePopup : MonoBehaviour
{
    public float lifetime = 1f;
    public float riseSpeed = 1.2f;
    public Color color = new Color(1f, 0.85f, 0.1f);

    Vector3 startPosition;
    float randomXDrift;
    float timer;
    Camera cam;
    TextMesh label;
    MeshRenderer meshRenderer;

    public static void Show(Vector3 worldPosition, float damageAmount)
    {
        int rounded = Mathf.RoundToInt(damageAmount);
        if(rounded <= 0) return;

        GameObject go = new GameObject("DamagePopup");
        DamagePopup popup = go.AddComponent<DamagePopup>();
        popup.Init(worldPosition, rounded);
    }

    void Init(Vector3 worldPosition, int damageAmount)
    {
        startPosition = worldPosition;
        randomXDrift = Random.Range(-0.4f, 0.4f);
        transform.position = worldPosition;
        cam = Camera.main;

        label = gameObject.AddComponent<TextMesh>();
        label.text = "-" + damageAmount;
        label.characterSize = 0.3f;
        label.fontSize = 100;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = color;

        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        meshRenderer.sortingOrder = 100;
    }

    void Update()
    {
        // Подстраховка (например, если компонент случайно повесили руками вместо того, чтобы
        // создавать через Show()) - отключаем только этот скрипт, а не уничтожаем весь
        // GameObject (если его повесили на машину, Destroy(gameObject) уничтожил бы саму машину).
        if(label == null){
            enabled = false;
            return;
        }

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / lifetime);

        transform.position = startPosition + new Vector3(randomXDrift * t, riseSpeed * timer, 0f);

        if(cam == null){
            cam = Camera.main;
        }
        if(cam != null){
            transform.rotation = cam.transform.rotation;
        }

        Color c = label.color;
        c.a = Mathf.Lerp(1f, 0f, t);
        label.color = c;

        if(timer >= lifetime){
            Destroy(gameObject);
        }
    }
}
