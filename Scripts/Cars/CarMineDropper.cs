using UnityEngine;

// Повесить на машину игрока (тот же объект, где PrometeoCarController). По нажатию dropKey
// оставляет позади машины мину (CarMine), которая взрывается, если на неё наедет другая машина.
[RequireComponent(typeof(PrometeoCarController))]
public class CarMineDropper : MonoBehaviour
{
    [Tooltip("Клавиша установки мины")]
    public KeyCode dropKey = KeyCode.F;
    [Tooltip("Точка появления мины (пустой дочерний объект позади машины). Если не назначена - мина ставится в позиции самой машины")]
    public Transform dropPoint;

    [Header("Настройки мины")]
    [Tooltip("Доля (0-1) от maxHealth цели, которую наносит взрыв мины")]
    [Range(0f, 1f)]
    public float damagePercent = 0.5f;

    [Tooltip("Префаб мины (визуальная модель). Если не назначен, используется простой цилиндр-заглушка")]
    [SerializeField] GameObject minePrefab;
    [Tooltip("Префаб эффекта взрыва (система частиц), проигрывается при подрыве мины")]
    [SerializeField] GameObject explosionEffect;
    [Tooltip("AudioSource со звуком установки мины - с уже назначенным в инспекторе AudioClip, как и остальные звуки в проекте")]
    [SerializeField] AudioSource dropSound;

    [Tooltip("Сколько мин за игру можно поставить (0 - нельзя ставить вообще)")]
    public int maxMines = 3;

    [Tooltip("Вероятность (0-1), что поставленная мина получит слой Obstacles - EnemyCarAI объезжает препятствия на этом слое (см. obstacleAvoidMask), поэтому такую мину боты будут стараться объехать, а не переехать в лоб")]
    [Range(0f, 1f)]
    public float obstacleLayerChance = 0.5f;

    public int minesLeft;

    PrometeoCarController car;

    void Awake()
    {
        car = GetComponent<PrometeoCarController>();
        minesLeft = maxMines;
    }

    void Update()
    {
        if(!car.enabled) return;

        // Input.GetKey* - это глобальный опрос клавиатуры, не привязанный к конкретному объекту.
        // Этот же компонент включён и у противников (см. CarRole) - без проверки isPlayerControlled
        // нажатие F игроком одновременно ставило бы мину со всех машин сразу. У противников
        // установкой мин управляет EnemyCarAI через TryDropMine(), а не эта клавиша.
        if(car.isPlayerControlled && Input.GetKeyDown(dropKey)){
            TryDropMine();
        }
    }

    // Публичная установка мины - используется и игроком (через Update() выше), и EnemyCarAI для
    // ботов. Сама проверяет, остались ли патроны мин и жива ли машина.
    public void TryDropMine()
    {
        if(!car.enabled || minesLeft <= 0) return;
        DropMine();
    }

    void DropMine()
    {
        minesLeft--;

        Vector3 position = dropPoint != null ? dropPoint.position : transform.position;
        Quaternion rotation = dropPoint != null ? dropPoint.rotation : transform.rotation;

        GameObject mineObj = minePrefab != null
            ? Instantiate(minePrefab, position, rotation)
            : CreateFallbackMine(position, rotation);

        CarMine mine = mineObj.GetComponent<CarMine>();
        if(mine == null) mine = mineObj.AddComponent<CarMine>();

        mine.damagePercent = damagePercent;
        mine.explosionEffectPrefab = explosionEffect;

        ApplyRandomObstacleLayer(mineObj);

        if(dropSound != null){
            dropSound.Play();
        }
    }

    // С вероятностью obstacleLayerChance переводит мину на слой Obstacles - EnemyCarAI по
    // умолчанию объезжает всё, что его лучи видят в obstacleAvoidMask (см. IsRealObstacle), но
    // мина стоит на земле на своём обычном слое и туда не попадает. Слой выставляем не только на
    // корневой объект, а рекурсивно на все дочерние - Physics.Raycast фильтрует по слою именно
    // того GameObject, на котором висит задетый коллайдер, а не по слою корня.
    void ApplyRandomObstacleLayer(GameObject mineObj)
    {
        if(Random.value >= obstacleLayerChance) return;

        int obstaclesLayer = LayerMask.NameToLayer("Obstacles");
        if(obstaclesLayer == -1){
            Debug.LogWarning("CarMineDropper: слой \"Obstacles\" не найден в проекте (Edit > Project Settings > Tags and Layers) - не могу перевести мину на этот слой.", this);
            return;
        }

        SetLayerRecursively(mineObj.transform, obstaclesLayer);
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach(Transform child in root){
            SetLayerRecursively(child, layer);
        }
    }

    // Заглушка на случай, если minePrefab не назначен - чтобы мины можно было ставить "из коробки"
    // ещё до того, как в проект добавят собственную модель мины.
    GameObject CreateFallbackMine(Vector3 position, Quaternion rotation)
    {
        GameObject mineObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mineObj.transform.SetPositionAndRotation(position, rotation);
        mineObj.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
        return mineObj;
    }
}
