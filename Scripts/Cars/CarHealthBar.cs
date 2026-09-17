using UnityEngine;
using UnityEngine.UI;

// Повесить на ту же машину, где висит CarHealth (игрок и/или противник). При старте сам строит
// World Space Canvas с полоской здоровья над машиной - ничего не нужно собирать в иерархии руками.
[RequireComponent(typeof(CarHealth))]
public class CarHealthBar : MonoBehaviour
{
    [Header("Позиция и размер (в мировых единицах)")]
    public Vector3 offset = new Vector3(0f, 2.2f, 0f);
    public Vector2 barSize = new Vector2(1.4f, 0.18f);

    [Header("Цвета")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    public Color fullHealthColor = Color.green;
    public Color lowHealthColor = Color.red;

    [Tooltip("Скрывать полоску, если здоровье полное")]
    public bool hideWhenFull = false;

    CarHealth health;
    Camera cam;
    GameObject barRoot;
    RectTransform canvasRect;
    Image fillImage;

    void Start()
    {
        health = GetComponent<CarHealth>();
        cam = Camera.main;
        BuildHealthBar();
    }

    void BuildHealthBar()
    {
        barRoot = new GameObject("HealthBarCanvas");
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = offset;

        Canvas canvas = barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        canvasRect = barRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = barSize;

        GameObject bgObject = new GameObject("Background");
        bgObject.transform.SetParent(barRoot.transform, false);
        Image bgImage = bgObject.AddComponent<Image>();
        bgImage.color = backgroundColor;
        StretchToParent(bgObject.GetComponent<RectTransform>());

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(barRoot.transform, false);
        fillImage = fillObject.AddComponent<Image>();
        // Image типа Filled без спрайта не может вычислить заливку и просто рисует весь
        // прямоугольник, игнорируя fillAmount - нужен хоть какой-то спрайт, даже сплошной белый.
        fillImage.sprite = GetWhiteSprite();
        fillImage.color = fullHealthColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        StretchToParent(fillObject.GetComponent<RectTransform>());
    }

    static Sprite whiteSprite;

    static Sprite GetWhiteSprite()
    {
        if(whiteSprite == null){
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return whiteSprite;
    }

    void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void LateUpdate()
    {
        if(health == null || canvasRect == null) return;

        if(health.isDead){
            barRoot.SetActive(false);
            return;
        }

        float ratio = Mathf.Clamp01(health.currentHealth / health.maxHealth);

        if(hideWhenFull && ratio >= 0.999f){
            barRoot.SetActive(false);
        }else{
            barRoot.SetActive(true);
            fillImage.fillAmount = ratio;
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, ratio);
        }

        // Полоска всегда развёрнута лицом к камере (billboard), иначе сбоку её не будет видно.
        if(cam == null){
            cam = Camera.main;
        }
        if(cam != null){
            canvasRect.rotation = cam.transform.rotation;
        }
    }
}
