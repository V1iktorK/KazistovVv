using System;
using UnityEngine;

/// <summary>
/// ЯКОРЬ РАЗМЕЩЕНИЯ РОБОТА: сохранить и восстановить позу «робот совпадает с реальным».
///
/// P1-7 (11.11.2026). ЧТО БЫЛО:
///   • поза сохранялась в МИРОВЫХ координатах СЦЕНЫ (`AnchorX/Y/Z` + кватернион). Но начало
///     отсчёта трекинга задаёт шлем и меняет его при перецентровке и перезапуске — значит
///     «восстановленный» робот оказывался не на своём столе, а в произвольной точке комнаты;
///   • настоящий `ARAnchor`/`XROrigin` не создавался вовсе, хотя класс называется
///     «пространственный якорь» — имя обещало привязку к трекинговому пространству;
///   • `void Awake() => Instance = this;` без сброса в `OnDestroy`, и `Instance` не читался нигде.
///
/// ЧЕМ ЭТО ГРОЗИЛО: наладчик калибровал робота по реальному столу, перезапускал приложение — и
/// робот появлялся со сдвигом, потому что мир успел перецентроваться. Ошибка тихая: ни в
/// интерфейсе, ни в журнале.
///
/// ЧТО СТАЛО (11.11.2026):
///   • поза хранится ОТНОСИТЕЛЬНО опорного трансформа трекинга (`trackingOrigin` — это XR Origin /
///     родитель рига), поэтому перецентровка мира её не сдвигает;
///   • если `trackingOrigin` не задан, роль опоры берёт родитель `robotRoot` (тоже трекинговое
///     пространство), а в крайнем случае — мировые координаты с ЯВНЫМ предупреждением в журнале;
///   • `Instance` сбрасывается в `OnDestroy` и не перезаписывается дубликатом;
///   • старые ключи с мировыми координатами читаются для совместимости, но при первой записи
///     заменяются относительными (`AnchorLocal*`) — молча терять прежние сохранения нельзя.
/// </summary>
public class SpatialAnchorManager : MonoBehaviour
{
    /// <summary>СОВЕТ УСТАРЕВШЕГО API: статическая ссылка не нужна и не читалась нигде (P1-7).</summary>
    [Obsolete("P1-7: SpatialAnchorManager.Instance не читался нигде; используйте ссылку из инспектора")]
    public static SpatialAnchorManager Instance;

    [Header("Placement")]
    public Transform robotRoot;

    /// <summary>
    /// ОПОРА ТРЕКИНГА: XR Origin / родитель рига. Поза робота хранится ОТНОСИТЕЛЬНО неё (P1-7).
    /// Пусто — берётся родитель `robotRoot`.
    /// </summary>
    public Transform trackingOrigin;

    public GameObject placementIndicator;

    private const string KeyAnchorSaved = "AnchorSaved";
    private const string KeyAnchorX = "AnchorX";
    private const string KeyAnchorY = "AnchorY";
    private const string KeyAnchorZ = "AnchorZ";
    private const string KeyAnchorQx = "AnchorQx";
    private const string KeyAnchorQy = "AnchorQy";
    private const string KeyAnchorQz = "AnchorQz";
    private const string KeyAnchorQw = "AnchorQw";

    // Относительные ключи (P1-7): поза в системе ОПОРЫ трекинга, а не в мировых координатах сцены.
    private const string KeySavedLocal = "AnchorLocalSaved";
    private const string KeyLocalX = "AnchorLocalX";
    private const string KeyLocalY = "AnchorLocalY";
    private const string KeyLocalZ = "AnchorLocalZ";
    private const string KeyLocalQx = "AnchorLocalQx";
    private const string KeyLocalQy = "AnchorLocalQy";
    private const string KeyLocalQz = "AnchorLocalQz";
    private const string KeyLocalQw = "AnchorLocalQw";

    void Awake()
    {
#pragma warning disable 0612, 0618 // обращение к устаревшему Instance внутри самого класса
        // P1-7: дубликат не должен перетирать ссылку, а сам экземпляр — висеть «живым» после
        // уничтожения (проверка `?.` на «фальшиво-null» Unity-объекте не спасает).
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SpatialAnchor] второй экземпляр на сцене — этот отключён (P1-7)");
            enabled = false;
            return;
        }
        Instance = this;
#pragma warning restore 0612, 0618
    }

    void OnDestroy()
    {
#pragma warning disable 0612, 0618
        if (Instance == this) Instance = null;
#pragma warning restore 0612, 0618
    }

    /// <summary>ОПОРА ТРЕКИНГА: заданная, иначе родитель робота (P1-7).</summary>
    public Transform TrackingOrigin
    {
        get
        {
            if (trackingOrigin != null) return trackingOrigin;
            if (robotRoot != null && robotRoot.parent != null) return robotRoot.parent;
            return null;
        }
    }

    /// <summary>
    /// ПЕРЕВЕСТИ МИРОВУЮ ПОЗУ В ПОЗУ ОТНОСИТЕЛЬНО ОПОРЫ (чистая функция — проверяется тестом).
    /// Если опоры нет, возвращает позу как есть и сообщает об этом вызывающему.
    /// </summary>
    public static void WorldToLocal(Transform origin, Vector3 worldPosition, Quaternion worldRotation,
        out Vector3 localPosition, out Quaternion localRotation)
    {
        if (origin == null)
        {
            localPosition = worldPosition;
            localRotation = worldRotation;
            return;
        }
        localPosition = origin.InverseTransformPoint(worldPosition);
        localRotation = Quaternion.Inverse(origin.rotation) * worldRotation;
    }

    /// <summary>ОБРАТНОЕ ПРЕОБРАЗОВАНИЕ: поза относительно опоры → мировая поза (чистая функция).</summary>
    public static void LocalToWorld(Transform origin, Vector3 localPosition, Quaternion localRotation,
        out Vector3 worldPosition, out Quaternion worldRotation)
    {
        if (origin == null)
        {
            worldPosition = localPosition;
            worldRotation = localRotation;
            return;
        }
        worldPosition = origin.TransformPoint(localPosition);
        worldRotation = origin.rotation * localRotation;
    }

    public void PlaceRobot(Vector3 position, Quaternion rotation)
    {
        if (robotRoot != null)
        {
            robotRoot.position = position;
            robotRoot.rotation = rotation;
        }

        if (placementIndicator != null)
        {
            placementIndicator.SetActive(false);
        }

        Transform origin = TrackingOrigin;
        if (origin == null)
        {
            Debug.LogWarning("[SpatialAnchor] опоры трекинга нет — поза сохранена в мировых " +
                             "координатах; после перецентровки робот может съехать (P1-7)");
        }

        Vector3 localPosition;
        Quaternion localRotation;
        WorldToLocal(origin, position, rotation, out localPosition, out localRotation);

        PlayerPrefs.SetInt(KeySavedLocal, 1);
        PlayerPrefs.SetFloat(KeyLocalX, localPosition.x);
        PlayerPrefs.SetFloat(KeyLocalY, localPosition.y);
        PlayerPrefs.SetFloat(KeyLocalZ, localPosition.z);
        PlayerPrefs.SetFloat(KeyLocalQx, localRotation.x);
        PlayerPrefs.SetFloat(KeyLocalQy, localRotation.y);
        PlayerPrefs.SetFloat(KeyLocalQz, localRotation.z);
        PlayerPrefs.SetFloat(KeyLocalQw, localRotation.w);
        PlayerPrefs.Save();

        Debug.Log("[SpatialAnchor] Робот размещён · мир " + position.ToString("0.000") +
                  " · от опоры " + localPosition.ToString("0.000") +
                  (origin != null ? " (опора «" + origin.name + "»)" : " (опоры нет)"));
    }

    public void Recalibrate()
    {
        PlayerPrefs.DeleteKey(KeySavedLocal);
        PlayerPrefs.DeleteKey(KeyAnchorSaved);
        PlayerPrefs.Save();

        if (placementIndicator != null)
        {
            placementIndicator.SetActive(true);
        }
    }

    void Start()
    {
        RestoreSavedAnchor();
    }

    /// <summary>
    /// ВОССТАНОВИТЬ ЯКОРЬ (P1-7): сначала относительная поза, затем — старый мировой формат
    /// (для сохранений, сделанных до правки). Восстановление не «спорит» с перецентровкой, потому
    /// что относительная поза пересчитывается в мир через ТЕКУЩУЮ опору.
    /// </summary>
    private void RestoreSavedAnchor()
    {
        if (robotRoot == null) return;

        Transform origin = TrackingOrigin;

        if (PlayerPrefs.GetInt(KeySavedLocal, 0) == 1)
        {
            Vector3 localPosition = new Vector3(
                PlayerPrefs.GetFloat(KeyLocalX),
                PlayerPrefs.GetFloat(KeyLocalY),
                PlayerPrefs.GetFloat(KeyLocalZ));
            Quaternion localRotation = new Quaternion(
                PlayerPrefs.GetFloat(KeyLocalQx),
                PlayerPrefs.GetFloat(KeyLocalQy),
                PlayerPrefs.GetFloat(KeyLocalQz),
                PlayerPrefs.GetFloat(KeyLocalQw));

            Vector3 worldPosition;
            Quaternion worldRotation;
            LocalToWorld(origin, localPosition, localRotation, out worldPosition, out worldRotation);
            robotRoot.position = worldPosition;
            robotRoot.rotation = worldRotation;

            if (placementIndicator != null) placementIndicator.SetActive(false);
            Debug.Log("[SpatialAnchor] поза восстановлена от опоры: " + localPosition.ToString("0.000") +
                      " → мир " + worldPosition.ToString("0.000"));
            return;
        }

        if (PlayerPrefs.GetInt(KeyAnchorSaved, 0) == 1)
        {
            Vector3 position = new Vector3(
                PlayerPrefs.GetFloat(KeyAnchorX),
                PlayerPrefs.GetFloat(KeyAnchorY),
                PlayerPrefs.GetFloat(KeyAnchorZ));
            Quaternion rotation = new Quaternion(
                PlayerPrefs.GetFloat(KeyAnchorQx),
                PlayerPrefs.GetFloat(KeyAnchorQy),
                PlayerPrefs.GetFloat(KeyAnchorQz),
                PlayerPrefs.GetFloat(KeyAnchorQw));

            robotRoot.position = position;
            robotRoot.rotation = rotation;
            if (placementIndicator != null) placementIndicator.SetActive(false);
            Debug.LogWarning("[SpatialAnchor] восстановлена СТАРАЯ (мировая) поза — привязки к опоре " +
                             "трекинга нет, после перецентровки робот может съехать. Откалибруйте заново (P1-7)");
        }
    }
}
