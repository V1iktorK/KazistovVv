// ================================================================================================
//  RegisteredObject.cs — МАРКЕР ОБЪЕКТОВ СЦЕНЫ ДЛЯ РЕЕСТРА (столы и т.п.).
//
//  ПОЧЕМУ ЭТОТ КЛАСС ОБЯЗАН ЖИТЬ В СВОЁМ ФАЙЛЕ (замер 12.11.2026 — дефект «Missing script»).
//  Раньше класс был объявлен внутри `RuntimeRegistry.cs`. У MonoBehaviour, объявленного не в
//  файле со своим именем, Unity НЕ создаёт MonoScript-ассет, и в сцену он пишется битой ссылкой
//  `m_Script: {fileID: <хеш>}` без guid + `m_EditorClassIdentifier`. Редактор разрешает класс по
//  идентификатору и показывает сцену целой, а Android/IL2CPP — нет: на устройствах журнал гудел
//  «The referenced script on this Behaviour (Game Object 'Стенд_1_Стол') is missing!», и компонент
//  не работал вовсе. В сценах XrMain/ArMain/MainScene висели 6 таких битых компонентов.
//
//  ПРАВИЛО: переносить/объединять этот класс с другими ЗАПРЕЩЕНО — вернётся битая сериализация,
//  которую редактор не покажет. Закреплено тестом `KvMonoScriptHygieneTests`.
// ================================================================================================

using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>
    /// Маркер на произвольных объектах сцены (столы и т.п.), чтобы реестр
    /// мог их найти и показать в дереве. Вешается автоматически при спавне.
    /// </summary>
    public class RegisteredObject : MonoBehaviour
    {
        // Runtime-only cache: заполняется при спавне (KvRobotImport/ObjectSpawner/StandBuilder)
        // и пересобирается RuntimeRegistry.RebuildFromScene(), поэтому сериализация ему НЕ нужна.
        // UAC1001 («Public field skipped by serialization…») гасится документированным способом —
        // [System.NonSerialized] (Unity Manual: Serialization rules analyzer reference, UAC1001).
        // Ставить сюда [System.Serializable] НЕЛЬЗЯ: ProjectNode держит рекурсивное поле
        // List<ProjectNode> Children (это deep self-reference cycle -> UAC1007/UAC1008),
        // нетипизируемый object Tag и bool? canHide (Unity их не сериализует), а также
        // не имеет конструктора без параметров. Это дало бы новые предупреждения и
        // раздуло бы сцену сериализованной копией дерева на каждом маркере.
        [System.NonSerialized, HideInInspector] public ProjectNode Node;
        public string DisplayName = "Объект";
    }
}
