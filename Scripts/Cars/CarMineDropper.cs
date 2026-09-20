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
        // нажатие F игроком одновременно ставило бы мину со всех машин сразу.
        if(!car.isPlayerControlled) return;

        if(minesLeft > 0 && Input.GetKeyDown(dropKey)){
            DropMine();
        }
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

        if(dropSound != null){
            dropSound.Play();
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
