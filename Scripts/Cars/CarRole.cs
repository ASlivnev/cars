using UnityEngine;
using UnityEngine.UI;

// Повесить на общий префаб машины (тот же, что и для игрока, и для противников). Галочка
// isPlayer решает, кем будет КОНКРЕТНЫЙ экземпляр этого префаба в сцене - вместо двух отдельных
// префабов "Player" и "Enemy" используется один, а роль выставляется на каждой копии в инспекторе.
//
// Настройка проверяется один раз в Awake(), до того как остальные скрипты на этом же объекте
// успеют запустить свой Start() - поэтому переключать isPlayer в рантайме смысла нет, это
// настройка конкретного экземпляра при расстановке машин на сцене, а не игровая механика.
[RequireComponent(typeof(PrometeoCarController))]
public class CarRole : MonoBehaviour
{
    [Tooltip("Включено - эта машина игрока (управляется с клавиатуры). Выключено - противник, управляется EnemyCarAI")]
    public bool isPlayer = true;

    [Tooltip("Имя объекта в сцене с UI Text спидометра - используется только для автопоиска (см. Awake), если Is Player включён, а Car Speed Text у PrometeoCarController не назначен вручную")]
    public string speedTextObjectName = "Speed Text";

    [Header("Опциональное вооружение")]
    [Tooltip("Включить нитро (форсаж) на этой машине")]
    public bool enableNitro = true;
    [Tooltip("Включить пулемёты на этой машине")]
    public bool enableMachineGuns = true;
    [Tooltip("Включить мины на этой машине")]
    public bool enableMines = true;
    [Tooltip("Объекты (например, стволы пулемётов на модели машины), которые нужно скрыть, если Enable Machine Guns выключен")]
    public GameObject[] hideWhenMachineGunsDisabled;

    PrometeoCarController car;

    void Awake()
    {
        car = GetComponent<PrometeoCarController>();

        // EnemyCarAI сам переключает PrometeoCarController.isPlayerControlled в false в своём
        // Start() - тут достаточно только включить/выключить сам компонент.
        EnemyCarAI ai = GetComponent<EnemyCarAI>();
        if(ai != null){
            ai.enabled = !isPlayer;
        }

        // Вооружение опционально и не зависит от роли (isPlayer) - можно, например, сделать
        // безоружного противника или безоружного игрока. Компоненты не удаляем, а выключаем -
        // EnemyCarAI при попытке им воспользоваться просто увидит null/выключенный компонент.
        CarNitro nitro = GetComponent<CarNitro>();
        if(nitro != null){
            nitro.enabled = enableNitro;
        }

        CarMachineGuns guns = GetComponent<CarMachineGuns>();
        if(guns != null){
            guns.enabled = enableMachineGuns;
        }

        if(!enableMachineGuns){
            foreach(GameObject obj in hideWhenMachineGunsDisabled){
                if(obj != null){
                    obj.SetActive(false);
                }
            }
        }

        CarMineDropper mineDropper = GetComponent<CarMineDropper>();
        if(mineDropper != null){
            mineDropper.enabled = enableMines;
        }

        // useUI/carSpeedText на префабе рассчитаны на одного игрока: useUI=true, но carSpeedText
        // ссылается на конкретный UI Text в сцене - у ботов эта ссылка пустая (её некому назначить,
        // Text один на сцену), и CarSpeedUI() падал с NullReferenceException на каждой машине-боте.
        // Раз этот же префаб теперь общий, UI скорости включаем только игроку.
        car.useUI = isPlayer;

        // Если это игрок и Car Speed Text не назначен руками - находим его сами по имени объекта
        // в сцене (GameObject.Find ищет один раз в Awake, не каждый кадр). Без этого пришлось бы
        // каждый раз при пересборке сцены заново перетаскивать ссылку на Text вручную.
        if(isPlayer && car.carSpeedText == null){
            GameObject speedTextObj = GameObject.Find(speedTextObjectName);
            if(speedTextObj != null){
                car.carSpeedText = speedTextObj.GetComponent<Text>();
            }
        }

        // AudioListener на префабе - только для игрока. Раз этот же префаб используется и для
        // ботов, при нескольких инстансах на сцене AudioListener окажется сразу на всех машинах,
        // а Unity поддерживает только один активный слушатель одновременно ("There are N audio
        // listeners in the scene").
        AudioListener audioListener = GetComponentInChildren<AudioListener>();
        if(audioListener != null){
            audioListener.enabled = isPlayer;
        }
    }
}
