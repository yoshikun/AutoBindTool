using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using BindData = AutoBindTool.BindData;

[CustomEditor(typeof(AutoBindTool))]
public class AutoBindToolInspector : Editor
{
    private AutoBindTool m_Target;

    private SerializedProperty m_BindDatas;
    private SerializedProperty m_BindComs;
    private List<BindData> m_TempList = new List<BindData>();
    private List<string> m_TempFiledNames = new List<string>();
    private List<string> m_TempComponentTypeNames = new List<string>();

    private string[] m_AssemblyNames = { "Assembly-CSharp" };
    private string[] m_HelperTypeNames;
    private string m_HelperTypeName;
    private int m_HelperTypeNameIndex;

    private AutoBindGlobalSetting m_Setting;

    private SerializedProperty m_Namespace;
    private SerializedProperty m_ClassName;
    private SerializedProperty m_CodePath;

    private void OnEnable()
    {
        m_Target = (AutoBindTool)target;
        m_BindDatas = serializedObject.FindProperty("BindDatas");
        m_BindComs = serializedObject.FindProperty("m_BindComs");

        m_HelperTypeNames = GetTypeNames(typeof(IAutoBindRuleHelper), m_AssemblyNames);

        string[] paths = AssetDatabase.FindAssets("t:AutoBindGlobalSetting");
        if (paths.Length == 0)
        {
            Debug.LogError("不存在AutoBindGlobalSetting");
            return;
        }
        if (paths.Length > 1)
        {
            Debug.LogError("AutoBindGlobalSetting数量大于1");
            return;
        }
        string path = AssetDatabase.GUIDToAssetPath(paths[0]);
        m_Setting = AssetDatabase.LoadAssetAtPath<AutoBindGlobalSetting>(path);


        m_Namespace = serializedObject.FindProperty("m_Namespace");
        m_ClassName = serializedObject.FindProperty("m_ClassName");
        m_CodePath = serializedObject.FindProperty("m_CodePath");

        m_Namespace.stringValue = string.IsNullOrEmpty(m_Namespace.stringValue) ? m_Setting.Namespace : m_Namespace.stringValue;
        m_ClassName.stringValue = string.IsNullOrEmpty(m_ClassName.stringValue) ? m_Target.gameObject.name : m_ClassName.stringValue;
        m_CodePath.stringValue = string.IsNullOrEmpty(m_CodePath.stringValue) ? m_Setting.CodePath : m_CodePath.stringValue;

        serializedObject.ApplyModifiedProperties();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawTopButton();

        DrawHelperSelect();

        DrawSetting();

        DrawKvData();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 绘制顶部按钮
    /// </summary>
    private void DrawTopButton()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("排序"))
        {
            Sort();
        }

        if (GUILayout.Button("全部删除"))
        {
            RemoveAll();
        }

        if (GUILayout.Button("删除空引用"))
        {
            RemoveNull();
        }

        if (GUILayout.Button("自动绑定组件"))
        {
            AutoBindComponent();
        }

        if (GUILayout.Button("生成绑定代码"))
        {
            AutoBindComponent();

            GenAutoBindCodeNew();

            AddComponentToGameObject();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void AddComponentToGameObject()
    {
        var componentName = m_Target.ClassName;
        var go = m_Target.gameObject;

        // 获取所有自定义组件的类型
        List<Type> customComponentTypes = new List<Type>();
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                // 检查类型是否为类，是否继承自UnityEngine.Component，并且名称与输入的组件名称匹配
                if (type.IsClass && type.IsSubclassOf(typeof(Component)) && type.Name == componentName)
                {
                    customComponentTypes.Add(type);
                }
            }
        }

        // 检查是否找到了匹配的组件类型
        if (customComponentTypes.Count > 0)
        {
            // 通常只取第一个匹配的类型，如果您有多个相同名称的组件，请确保使用正确的命名空间
            Type componentType = customComponentTypes[0];

            if (go.GetComponent(componentType) == null)
            {
                Component component = go.AddComponent(componentType);
            }
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Cannot find component type: " + componentName, "OK");
        }
    }

    /// <summary>
    /// 排序
    /// </summary>
    private void Sort()
    {


        m_TempList.Clear();
        foreach (BindData data in m_Target.BindDatas)
        {
            m_TempList.Add(new BindData(data.Name, data.BindCom));
        }
        m_TempList.Sort((x, y) =>
        {
            return string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        });

        m_BindDatas.ClearArray();
        foreach (BindData data in m_TempList)
        {
            AddBindData(data.Name, data.BindCom);
        }

        SyncBindComs();
    }

    /// <summary>
    /// 全部删除
    /// </summary>
    private void RemoveAll()
    {
        m_BindDatas.ClearArray();

        SyncBindComs();
    }

    /// <summary>
    /// 删除空引用
    /// </summary>
    private void RemoveNull()
    {
        for (int i = m_BindDatas.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty element = m_BindDatas.GetArrayElementAtIndex(i).FindPropertyRelative("BindCom");
            if (element.objectReferenceValue == null)
            {
                m_BindDatas.DeleteArrayElementAtIndex(i);
            }
        }

        SyncBindComs();
    }

    /// <summary>
    /// 自动绑定组件
    /// </summary>
    private void AutoBindComponent()
    {
        m_BindDatas.ClearArray();

        Transform[] childs = m_Target.gameObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in childs)
        {
            if (child.GetComponent<AutoBindTool>())
            {
                continue;
            }

            var parent = child.GetComponentInParent<AutoBindTool>();
            if (parent != m_Target)
            {
                continue;
            }

            m_TempFiledNames.Clear();
            m_TempComponentTypeNames.Clear();

            if (m_Target.RuleHelper.IsValidBind(child, m_TempFiledNames, m_TempComponentTypeNames))
            {
                for (int i = 0; i < m_TempFiledNames.Count; i++)
                {
                    Component com = child.GetComponent(m_TempComponentTypeNames[i]);
                    if (com == null)
                    {
                        Debug.LogError($"{child.name}上不存在{m_TempComponentTypeNames[i]}的组件");
                    }
                    else
                    {
                        AddBindData(m_TempFiledNames[i], child.GetComponent(m_TempComponentTypeNames[i]));
                    }

                }
            }
        }

        SyncBindComs();
    }

    /// <summary>
    /// 绘制辅助器选择框
    /// </summary>
    private void DrawHelperSelect()
    {
        m_HelperTypeName = m_HelperTypeNames[0];

        if (m_Target.RuleHelper != null)
        {
            m_HelperTypeName = m_Target.RuleHelper.GetType().Name;

            for (int i = 0; i < m_HelperTypeNames.Length; i++)
            {
                if (m_HelperTypeName == m_HelperTypeNames[i])
                {
                    m_HelperTypeNameIndex = i;
                }
            }
        }
        else
        {
            IAutoBindRuleHelper helper = (IAutoBindRuleHelper)CreateHelperInstance(m_HelperTypeName, m_AssemblyNames);
            m_Target.RuleHelper = helper;
        }

        foreach (GameObject go in Selection.gameObjects)
        {
            AutoBindTool autoBindTool = go.GetComponent<AutoBindTool>();
            if (autoBindTool.RuleHelper == null)
            {
                IAutoBindRuleHelper helper = (IAutoBindRuleHelper)CreateHelperInstance(m_HelperTypeName, m_AssemblyNames);
                autoBindTool.RuleHelper = helper;
            }
        }

        int selectedIndex = EditorGUILayout.Popup("AutoBindRuleHelper", m_HelperTypeNameIndex, m_HelperTypeNames);
        if (selectedIndex != m_HelperTypeNameIndex)
        {
            m_HelperTypeNameIndex = selectedIndex;
            m_HelperTypeName = m_HelperTypeNames[selectedIndex];
            IAutoBindRuleHelper helper = (IAutoBindRuleHelper)CreateHelperInstance(m_HelperTypeName, m_AssemblyNames);
            m_Target.RuleHelper = helper;

        }
    }

    /// <summary>
    /// 绘制设置项
    /// </summary>
    private void DrawSetting()
    {
        EditorGUILayout.BeginHorizontal();
        m_Namespace.stringValue = EditorGUILayout.TextField(new GUIContent("命名空间："), m_Namespace.stringValue);
        if (GUILayout.Button("默认设置"))
        {
            m_Namespace.stringValue = m_Setting.Namespace;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        m_ClassName.stringValue = EditorGUILayout.TextField(new GUIContent("类名："), m_ClassName.stringValue);
        if (GUILayout.Button("物体名"))
        {
            m_ClassName.stringValue = m_Target.gameObject.name;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("代码保存路径：");
        EditorGUILayout.LabelField(m_CodePath.stringValue);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("选择路径"))
        {
            string temp = m_CodePath.stringValue;
            m_CodePath.stringValue = EditorUtility.OpenFolderPanel("选择代码保存路径", Application.dataPath, "");
            if (string.IsNullOrEmpty(m_CodePath.stringValue))
            {
                m_CodePath.stringValue = temp;
            }
        }
        if (GUILayout.Button("默认设置"))
        {
            m_CodePath.stringValue = m_Setting.CodePath;
        }
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制键值对数据
    /// </summary>
    private void DrawKvData()
    {
        //绘制key value数据

        int needDeleteIndex = -1;

        EditorGUILayout.BeginVertical();
        SerializedProperty property;

        for (int i = 0; i < m_BindDatas.arraySize; i++)
        {

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(25));
            property = m_BindDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Name");
            property.stringValue = EditorGUILayout.TextField(property.stringValue, GUILayout.Width(150));
            property = m_BindDatas.GetArrayElementAtIndex(i).FindPropertyRelative("BindCom");
            property.objectReferenceValue = EditorGUILayout.ObjectField(property.objectReferenceValue, typeof(Component), true);

            if (GUILayout.Button("X"))
            {
                //将元素下标添加进删除list
                needDeleteIndex = i;
            }
            EditorGUILayout.EndHorizontal();
        }

        //删除data
        if (needDeleteIndex != -1)
        {
            m_BindDatas.DeleteArrayElementAtIndex(needDeleteIndex);
            SyncBindComs();
        }

        EditorGUILayout.EndVertical();
    }



    /// <summary>
    /// 添加绑定数据
    /// </summary>
    private void AddBindData(string name, Component bindCom)
    {
        int index = m_BindDatas.arraySize;
        m_BindDatas.InsertArrayElementAtIndex(index);
        SerializedProperty element = m_BindDatas.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("Name").stringValue = name;
        element.FindPropertyRelative("BindCom").objectReferenceValue = bindCom;

    }

    /// <summary>
    /// 同步绑定数据
    /// </summary>
    private void SyncBindComs()
    {
        m_BindComs.ClearArray();

        for (int i = 0; i < m_BindDatas.arraySize; i++)
        {
            SerializedProperty property = m_BindDatas.GetArrayElementAtIndex(i).FindPropertyRelative("BindCom");
            m_BindComs.InsertArrayElementAtIndex(i);
            m_BindComs.GetArrayElementAtIndex(i).objectReferenceValue = property.objectReferenceValue;
        }
    }

    /// <summary>
    /// 获取指定基类在指定程序集中的所有子类名称
    /// </summary>
    private string[] GetTypeNames(Type typeBase, string[] assemblyNames)
    {
        List<string> typeNames = new List<string>();
        foreach (string assemblyName in assemblyNames)
        {
            Assembly assembly = null;
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch
            {
                continue;
            }

            if (assembly == null)
            {
                continue;
            }

            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                if (type.IsClass && !type.IsAbstract && typeBase.IsAssignableFrom(type))
                {
                    typeNames.Add(type.FullName);
                }
            }
        }

        typeNames.Sort();
        return typeNames.ToArray();
    }

    /// <summary>
    /// 创建辅助器实例
    /// </summary>
    private object CreateHelperInstance(string helperTypeName, string[] assemblyNames)
    {
        foreach (string assemblyName in assemblyNames)
        {
            Assembly assembly = Assembly.Load(assemblyName);

            object instance = assembly.CreateInstance(helperTypeName);
            if (instance != null)
            {
                return instance;
            }
        }

        return null;
    }


    /// <summary>
    /// 生成自动绑定代码
    /// </summary>
    //private void GenAutoBindCode()
    //{
    //    GameObject go = m_Target.gameObject;

    //    string className = !string.IsNullOrEmpty(m_Target.ClassName) ? m_Target.ClassName : go.name;
    //    string codePath = !string.IsNullOrEmpty(m_Target.CodePath) ? m_Target.CodePath : m_Setting.CodePath;

    //    if (!Directory.Exists(codePath))
    //    {
    //        Debug.LogError($"{go.name}的代码保存路径{codePath}无效");
    //    }

    //    using (StreamWriter sw = new StreamWriter($"{codePath}/{className}.BindComponents.cs"))
    //    {
    //        sw.WriteLine("using UnityEngine;");
    //        sw.WriteLine("using UnityEngine.UI;");
    //        sw.WriteLine("");

    //        sw.WriteLine("//自动生成于：" + DateTime.Now);

    //        if (!string.IsNullOrEmpty(m_Target.Namespace))
    //        {
    //            //命名空间
    //            sw.WriteLine("namespace " + m_Target.Namespace);
    //            sw.WriteLine("{");
    //            sw.WriteLine("");
    //        }

    //        //类名
    //        sw.WriteLine($"\tpublic partial class {className}");
    //        sw.WriteLine("\t{");
    //        sw.WriteLine("");

    //        //组件字段
    //        foreach (BindData data in m_Target.BindDatas)
    //        {
    //            sw.WriteLine($"\t\tprivate {data.BindCom.GetType().Name} m_{data.Name};");
    //        }
    //        sw.WriteLine("");

    //        sw.WriteLine("\t\tprivate void GetBindComponents(GameObject go)");
    //        sw.WriteLine("\t\t{");

    //        //获取autoBindTool上的Component
    //        sw.WriteLine($"\t\t\tvar autoBindTool = go.GetComponent<AutoBindTool>();");
    //        sw.WriteLine("");

    //        //根据索引获取

    //        for (int i = 0; i < m_Target.BindDatas.Count; i++)
    //        {
    //            BindData data = m_Target.BindDatas[i];
    //            string filedName = $"m_{data.Name}";
    //            sw.WriteLine($"\t\t\t{filedName} = autoBindTool.GetBindComponent<{data.BindCom.GetType().Name}>({i});");
    //        }

    //        sw.WriteLine("\t\t}");

    //        sw.WriteLine("\t}");

    //        if (!string.IsNullOrEmpty(m_Target.Namespace))
    //        {
    //            sw.WriteLine("}");
    //        }
    //    }

    //    AssetDatabase.Refresh();
    //    EditorUtility.DisplayDialog("提示", "代码生成完毕", "OK");
    //}

    // 解析文件内容，定位到组件字段部分进行修改
    // 假设组件字段位于特定的标记注释之间，例如 /* COMPONENT FIELDS */
    string componentFieldsStartMarker = "/* COMPONENT FIELDS */";
    string componentFieldsEndMarker = "/* COMPONENT FIELDS END */";

    private void GenAutoBindCodeNew()
    {
        GameObject go = m_Target.gameObject;

        string className = !string.IsNullOrEmpty(m_Target.ClassName) ? m_Target.ClassName : go.name;
        string codePath = !string.IsNullOrEmpty(m_Target.CodePath) ? m_Target.CodePath : m_Setting.CodePath;
        string filePath = $"{codePath}/{className}.cs";

        // 确保代码保存路径存在，如果不存在则创建
        if (!Directory.Exists(codePath))
        {
            Directory.CreateDirectory(codePath);
        }

        var hasNamespace = !string.IsNullOrEmpty(m_Target.Namespace);
        var extraTab = hasNamespace ? "\t" : "";

        // 检查文件是否存在
        if (File.Exists(filePath))
        {
            // 文件存在，读取文件内容
            string code = File.ReadAllText(filePath);

            int startIndex = code.IndexOf(componentFieldsStartMarker);

            // 没有生成标记就创建标记
            if (startIndex == -1)
            {
                startIndex = code.IndexOf("{");
                if (hasNamespace)
                {
                    // 查找第二个括号
                    startIndex = code.IndexOf("{", startIndex + 1);
                }

                if (startIndex != -1)
                {
                    // 需要把括号截取到前面
                    var startLength = startIndex + 1;
                    var endIndex = startIndex + 1;

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("");
                    // 增加生成开始标记
                    sb.AppendLine($"\t{extraTab}{componentFieldsStartMarker}");

                    // 增加生成结束标记
                    sb.AppendLine($"\t{extraTab}{componentFieldsEndMarker}");

                    code = code.Substring(0, startLength) + sb.ToString() + code.Substring(endIndex);
                }
            }

            startIndex = code.IndexOf(componentFieldsStartMarker);
            if (startIndex != -1)
            {
                var startLength = startIndex + componentFieldsStartMarker.Length + 1;
                // 找到结束标记
                int endIndex = code.IndexOf(componentFieldsEndMarker, startIndex);
                if (endIndex != -1)
                {
                    var endMarkerTab = $"\t{extraTab}";
                    endIndex -= endMarkerTab.Length + 1;

                    // 替换组件字段部分
                    string newComponentFields = GenerateComponentFields(extraTab);
                    code = code.Substring(0, startLength) + newComponentFields + code.Substring(endIndex);
                }
            }

            // 写入修改后的内容到文件
            File.WriteAllText(filePath, code);
        }
        else
        {
            // 文件不存在，创建新文件并写入内容
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine("using UnityEngine;");
                sw.WriteLine("using UnityEngine.UI;");
                sw.WriteLine("");

                //sw.WriteLine("//自动生成于：" + DateTime.Now);

                if (hasNamespace)
                {
                    //命名空间
                    sw.WriteLine("namespace " + m_Target.Namespace);
                    sw.WriteLine("{");
                    sw.WriteLine("");
                }

                //类名
                sw.WriteLine($"{extraTab}public class {className} : AutoBindView");
                sw.WriteLine($"{extraTab}{{");
                sw.WriteLine("");

                // 增加生成开始标记
                sw.WriteLine($"\t{extraTab}{componentFieldsStartMarker}");

                // 生成绑定
                sw.WriteLine(GenerateComponentFields(extraTab));

                // 增加生成结束标记
                sw.WriteLine($"\t{extraTab}{componentFieldsEndMarker}");

                // 类结尾
                sw.WriteLine($"{extraTab}}}");

                if (hasNamespace)
                {
                    sw.WriteLine("}");
                }
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("提示", "代码生成完毕", "OK");
    }

    private string GenerateComponentFields(string extraTab)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("");

        foreach (BindData data in m_Target.BindDatas)
        {
            sb.AppendLine($"\t{extraTab}private {data.BindCom.GetType().Name} m_{data.Name};");
        }

        sb.AppendLine("");

        // 生成绑定
        sb.AppendLine($"\t{extraTab}protected override void BindComponents(GameObject go)");
        sb.AppendLine($"\t{extraTab}{{");

        //获取autoBindTool上的Component
        sb.AppendLine($"\t\t{extraTab}var autoBindTool = go.GetComponent<AutoBindTool>();");
        sb.AppendLine("");

        //根据索引获取
        for (int i = 0; i < m_Target.BindDatas.Count; i++)
        {
            BindData data = m_Target.BindDatas[i];
            string filedName = $"m_{data.Name}";
            sb.AppendLine($"\t\t{extraTab}{filedName} = autoBindTool.BindComponents<{data.BindCom.GetType().Name}>({i});");
        }

        sb.AppendLine($"\t{extraTab}}}");

        return sb.ToString();
    }

}
