using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Повесить на отдельный пустой объект в сцене (один на сцену). Следит за здоровьем игрока и
// количеством живых противников: если игрок погиб - показывает экран поражения, если уничтожены
// все противники - экран победы. Заодно даёт публичную функцию перезапуска сцены (например,
// для кнопки "Restart" на экране Win/Lose).
public class GameManager : MonoBehaviour
{
    [Tooltip("CarHealth машины игрока")]
    public CarHealth playerHealth;

    [Header("UI")]
    [Tooltip("Экран поражения - показывается, когда погибает игрок")]
    public GameObject loseUI;
    [Tooltip("Экран победы - показывается, когда уничтожены все противники")]
    public GameObject winUI;

    [Tooltip("Как часто (сек) проверять, не уничтожены ли уже все противники")]
    public float winCheckInterval = 0.5f;

    bool isGameOver;
    float winCheckTimer;
    List<CarHealth> trackedEnemies;

    void Start()
    {
        if(loseUI != null) loseUI.SetActive(false);
        if(winUI != null) winUI.SetActive(false);

        // Игрок и противники - один и тот же префаб (см. CarRole), поэтому машину игрока нельзя
        // найти по типу компонентов - только по CarRole.isEnemy == false. Ищем сами, если ссылку
        // не задали вручную в инспекторе.
        if(playerHealth == null){
            FindPlayerHealth();
        }

        // Запоминаем противников СЕЙЧАС, пока никто ещё не умер. CarHealth.Die() отключает
        // PrometeoCarController (car.enabled = false), а у него на OnDisable() машина
        // удаляется из PrometeoCarController.AllCars - то есть погибшие противники полностью
        // пропадают из этого списка, а не просто помечаются мёртвыми. Поэтому проверять победу
        // напрямую через AllCars нельзя - в момент гибели последнего врага список противников
        // в нём уже был бы пуст, и условие "все враги мертвы" никогда бы не сработало.
        trackedEnemies = new List<CarHealth>();
        foreach(PrometeoCarController candidate in PrometeoCarController.AllCars){
            if(candidate == null) continue;

            // Компонент EnemyCarAI есть теперь у ВСЕХ машин, включая игрока (просто выключен) -
            // проверять его наличие больше нельзя, нужна именно роль через CarRole.isEnemy,
            // иначе игрок сам попадёт в список "противников".
            CarRole role = candidate.GetComponent<CarRole>();
            if(role == null || !role.isEnemy) continue;

            CarHealth health = candidate.GetComponent<CarHealth>();
            if(health != null){
                trackedEnemies.Add(health);
            }
        }
    }

    // См. комментарий в Start() - ищет среди всех машин на сцене ту, у которой CarRole.isEnemy
    // == false (по той же логике, что и CameraFollow.FindPlayerCar).
    void FindPlayerHealth()
    {
        foreach(PrometeoCarController candidate in PrometeoCarController.AllCars){
            if(candidate == null) continue;

            CarRole role = candidate.GetComponent<CarRole>();
            if(role != null && role.isEnemy) continue;

            CarHealth health = candidate.GetComponent<CarHealth>();
            if(health != null){
                playerHealth = health;
                return;
            }
        }
    }

    void Update()
    {
        if(isGameOver) return;

        if(playerHealth != null && playerHealth.isDead){
            ShowLose();
            return;
        }

        winCheckTimer -= Time.deltaTime;
        if(winCheckTimer <= 0f){
            winCheckTimer = winCheckInterval;
            if(AllEnemiesDead()){
                ShowWin();
            }
        }
    }

    // Проверяем заранее сохранённый список противников (см. Start), а не текущий
    // PrometeoCarController.AllCars - погибшие машины из AllCars уже удалены.
    bool AllEnemiesDead()
    {
        if(trackedEnemies.Count == 0) return false;

        foreach(CarHealth health in trackedEnemies){
            if(health != null && !health.isDead){
                return false;
            }
        }
        return true;
    }

    void ShowLose()
    {
        isGameOver = true;
        if(loseUI != null) loseUI.SetActive(true);
        // Управление игрока уже отключено самой CarHealth.Die() (car.enabled = false) -
        // здесь дополнительно ничего выключать не нужно.
    }

    void ShowWin()
    {
        isGameOver = true;
        if(winUI != null) winUI.SetActive(true);

        // При победе, в отличие от поражения, машина игрока жива - её никто не отключал,
        // поэтому управление глушим явно здесь. car.enabled = false заодно останавливает и
        // CarNitro/CarMachineGuns/CarMineDropper - они сами проверяют car.enabled в Update().
        if(playerHealth != null){
            PrometeoCarController playerCar = playerHealth.GetComponent<PrometeoCarController>();
            if(playerCar != null){
                // WheelCollider - отдельная физическая симуляция и продолжает крутить колёса с
                // последним заданным motorTorque даже после car.enabled = false (это останавливает
                // только Update(), а не саму физику подвески) - поэтому газ снимаем явно.
                playerCar.ThrottleOff();
                playerCar.enabled = false;
            }
        }
    }

    // Публичная функция перезагрузки текущей сцены - повесить на OnClick() кнопки перезапуска.
    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
