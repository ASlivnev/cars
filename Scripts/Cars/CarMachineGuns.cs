using UnityEngine;

// Повесить на машину игрока (тот же объект, где PrometeoCarController). Два пулемёта на
// передней части машины стреляют одновременно по удержанию fireKey. Каждая попавшая пуля
// отнимает 1hp у CarHealth цели (кроме самой машины-стрелка).
[RequireComponent(typeof(PrometeoCarController))]
public class CarMachineGuns : MonoBehaviour
{
    [Tooltip("Клавиша/кнопка стрельбы - можно удерживать, огонь идёт очередью по скорострельности. По умолчанию слева на клавиатуре (не мышь и не ПКМ), т.к. руль и газ висят на стрелочках справа - одной и той же рукой одновременно рулить и стрелять мышью не получится")]
    public KeyCode fireKey = KeyCode.LeftControl;

    [Tooltip("Точка левого пулемёта на передней части машины (пустой дочерний объект на капоте)")]
    public Transform leftGunPoint;
    [Tooltip("Точка правого пулемёта на передней части машины")]
    public Transform rightGunPoint;

    [Header("Настройки стрельбы")]
    [Tooltip("Общее количество патронов на оба пулемёта")]
    public int maxAmmo = 200;
    [Tooltip("Скорострельность - сколько раз в секунду стреляют оба пулемёта одновременно")]
    public float shotsPerSecond = 8f;
    [Tooltip("Длина полёта пули (метры) - после этого расстояния пуля исчезает, даже если ни во что не попала")]
    public float bulletRange = 60f;
    [Tooltip("Скорость полёта пули (м/с)")]
    public float bulletSpeed = 120f;

    [Tooltip("Префаб пули (визуальная модель). Если не назначен, используется простая капсула-заглушка")]
    [SerializeField] GameObject bulletPrefab;
    [Tooltip("AudioSource со звуком выстрела - с назначенным в инспекторе AudioClip, как carEngineSound/tireScreechSound в PrometeoCarController. Проигрывается через PlayOneShot на каждый залп, а не Play() - иначе на высокой скорострельности каждый следующий выстрел обрывал бы звук предыдущего")]
    [SerializeField] AudioSource fireSound;

    const int damagePerBullet = 1;

    public int ammoLeft;

    PrometeoCarController car;
    float fireTimer;

    void Awake()
    {
        car = GetComponent<PrometeoCarController>();
        ammoLeft = maxAmmo;
    }

    void Update()
    {
        if(!car.enabled) return;

        fireTimer -= Time.deltaTime;

        if(Input.GetKey(fireKey) && fireTimer <= 0f && ammoLeft > 0){
            fireTimer = 1f / shotsPerSecond;
            Fire();
        }
    }

    void Fire()
    {
        SpawnBullet(leftGunPoint);
        SpawnBullet(rightGunPoint);

        if(fireSound != null){
            fireSound.PlayOneShot(fireSound.clip);
        }
    }

    void SpawnBullet(Transform firePoint)
    {
        if(firePoint == null || ammoLeft <= 0) return;
        ammoLeft--;

        GameObject bulletObj = bulletPrefab != null
            ? Instantiate(bulletPrefab, firePoint.position, firePoint.rotation)
            : CreateFallbackBullet(firePoint);

        CarBullet bullet = bulletObj.GetComponent<CarBullet>();
        if(bullet == null) bullet = bulletObj.AddComponent<CarBullet>();

        bullet.speed = bulletSpeed;
        bullet.maxDistance = bulletRange;
        bullet.damage = damagePerBullet;
        bullet.shooterRoot = transform.root;
    }

    // Заглушка на случай, если bulletPrefab не назначен - чтобы пулемёты стреляли "из коробки"
    // ещё до того, как в проект добавят собственную модель пули.
    GameObject CreateFallbackBullet(Transform firePoint)
    {
        GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bulletObj.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation * Quaternion.Euler(90f, 0f, 0f));
        bulletObj.transform.localScale = new Vector3(0.05f, 0.15f, 0.05f);

        // Коллайдер не нужен - попадание считает CarBullet через Physics.Raycast, а не через
        // физическое столкновение. Важно: Destroy() удаляет компонент только в конце кадра, а
        // пуля спавнится вплотную к корпусу машины (и физически пересекается с её же коллайдером
        // в момент создания) - пока коллайдер ещё жив, физика успевает "вытолкнуть" пересекающиеся
        // объекты друг из друга, и машина при каждом выстреле дёргается назад, как от отдачи.
        // enabled = false отключает коллайдер мгновенно, до следующего шага физики.
        Collider bulletCollider = bulletObj.GetComponent<Collider>();
        bulletCollider.enabled = false;
        Destroy(bulletCollider);

        return bulletObj;
    }
}
