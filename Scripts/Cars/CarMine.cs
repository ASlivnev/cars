using UnityEngine;

// Мина, оставленная машиной через CarMineDropper. Взрывается, когда на неё наезжает ЛЮБАЯ
// машина - включая ту, что её поставила. Коллайдер - триггер: мина не должна физически толкать
// машину как препятствие, только регистрировать наезд.
[RequireComponent(typeof(Collider))]
public class CarMine : MonoBehaviour
{
    [Tooltip("Через сколько секунд после установки мина взводится и начинает реагировать на наезд. Нужна не для защиты владельца навсегда, а только чтобы мина не срабатывала мгновенно от коллайдера самой машины, которая её только что поставила и в этот момент стоит прямо на ней")]
    public float armDelay = 1f;

    [HideInInspector] public float damagePercent;
    [HideInInspector] public GameObject explosionEffectPrefab;

    [Tooltip("AudioSource со звуком взрыва - с уже назначенным в инспекторе AudioClip, как и остальные звуки в проекте (explosionSound/collisionSound у CarHealth и т.д.). Настраивается прямо на префабе мины")]
    [SerializeField] AudioSource explosionSound;

    float armTime;

    void Awake()
    {
        // isTrigger выставляем здесь, а не полагаемся на настройку в префабе - если забыть
        // поставить галочку на префабе мины, она бы толкала машины физикой как обычная стена.
        GetComponent<Collider>().isTrigger = true;
        armTime = Time.time + armDelay;
    }

    void OnTriggerEnter(Collider other)
    {
        if(Time.time < armTime) return;

        CarHealth health = other.GetComponentInParent<CarHealth>();
        if(health == null || health.isDead) return;

        health.TakeDamage(health.maxHealth * damagePercent);
        Explode();
    }

    void Explode()
    {
        if(explosionEffectPrefab != null){
            GameObject explosion = Instantiate(explosionEffectPrefab, transform.position, transform.rotation);
            Destroy(explosion, 2f);
        }

        // Коллайдер и меши прячем сразу - взорвавшаяся мина не должна сработать повторно или
        // висеть в воздухе, пока играет звук. Сам объект уничтожаем с задержкой на длину клипа,
        // иначе обычный Destroy(gameObject) этим же кадром оборвал бы Play(), не дав ему начаться.
        GetComponent<Collider>().enabled = false;
        foreach(Renderer meshRenderer in GetComponentsInChildren<Renderer>()){
            meshRenderer.enabled = false;
        }

        float destroyDelay = 0f;
        if(explosionSound != null){
            explosionSound.Play();
            destroyDelay = explosionSound.clip != null ? explosionSound.clip.length : 0f;
        }

        Destroy(gameObject, destroyDelay);
    }
}
