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

        // Input.GetKey* - это глобальный опрос клавиатуры, не привязанный к конкретному объекту.
        // Этот же компонент включён и у противников (см. CarRole) - без проверки isPlayerControlled
        // удержание ЛКМ игроком одновременно открывало бы огонь со всех машин сразу. У противников
        // стрельбой управляет EnemyCarAI через TryFire(), а не эта клавиша.
        if(car.isPlayerControlled && Input.GetKey(fireKey)){
            TryFire();
        }
    }

    // Публичный "спуск курка" на один кадр - используется и игроком (через Update() выше), и
    // EnemyCarAI для ботов. Сам разбирается, можно ли стрелять прямо сейчас (боеприпасы,
    // скорострельность), поэтому вызывающему достаточно звать этот метод, пока хочет стрелять,
    // как при обычном удержании кнопки.
    public void TryFire()
    {
        // enabled - на случай, если этот компонент выключен через CarRole (опциональное
        // вооружение). Вызов публичного метода не блокируется выключенным компонентом сам по
        // себе (это влияет только на автоматические Update/FixedUpdate), поэтому проверяем сами.
        if(!enabled || !car.enabled || fireTimer > 0f || ammoLeft <= 0) return;

        fireTimer = 1f / shotsPerSecond;
        Fire();
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

        StripPhysicsComponents(bulletObj);

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
        return bulletObj;
    }

    // Пуля движется и наносит урон вручную через CarBullet (Physics.Raycast), поэтому Collider
    // и Rigidbody ей не нужны - ни на заглушке, ни на пользовательском bulletPrefab. Важно не
    // просто их игнорировать, а именно убрать: пуля спавнится вплотную к корпусу машины (точка
    // вылета стоит на капоте) и физически пересекается с её же коллайдером в момент создания.
    // Пока чужой коллайдер жив хотя бы один физический шаг, PhysX "выталкивает" пересекающиеся
    // объекты друг из друга - а так как у пули нет Rigidbody, весь этот импульс достаётся машине,
    // из-за чего газ/руль глохнут при каждом выстреле. Destroy() удаляет компонент только в конце
    // кадра, поэтому сначала выключаем коллайдер мгновенно (enabled = false), а Destroy() потом
    // просто подчищает компонент за ненадобностью - тот же приём, что и в CarHealth.CleanupWreck().
    void StripPhysicsComponents(GameObject bulletObj)
    {
        foreach(Collider col in bulletObj.GetComponentsInChildren<Collider>()){
            col.enabled = false;
            Destroy(col);
        }
        foreach(Rigidbody rb in bulletObj.GetComponentsInChildren<Rigidbody>()){
            Destroy(rb);
        }
    }
}
