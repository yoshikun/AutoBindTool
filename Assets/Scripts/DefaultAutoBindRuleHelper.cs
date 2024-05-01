﻿using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 默认自动绑定规则辅助器
/// </summary>
public class DefaultAutoBindRuleHelper : IAutoBindRuleHelper
{
    /// <summary>
    /// 命名前缀与类型的映射
    /// </summary>
    private Dictionary<string, string> m_PrefixesDict = new Dictionary<string, string>()
    {
        {"GameObject","GameObject"},
        {"Transform","Transform"},
        {"Animation","Animation"},
        {"Animator","Animator"},

        {"RectTransform","RectTransform"},
        {"Canvas","Canvas"},
        {"Group","CanvasGroup"},
        {"VGroup","VerticalLayoutGroup"},
        {"HGroup","HorizontalLayoutGroup"},
        {"GGroup","GridLayoutGroup"},
        {"TGroup","ToggleGroup"},

        {"Button","Button"},
        {"Image","Image"},
        {"RawImage","RawImage"},
        {"Text","Text"},
        {"InputField","InputField"},
        {"Slider","Slider"},
        {"Mask","Mask"},
        {"RectMask2D","RectMask2D"},
        {"Toggle","Toggle"},
        {"Scrollbar","Scrollbar"},
        {"ScrollRect","ScrollRect"},
        {"Dropdown","Dropdown"},
    };

    public bool IsValidBind(Transform target, List<string> filedNames, List<string> componentTypeNames)
    {
        string[] strArray = target.name.Split('_');

        if (strArray.Length == 1)
        {
            return false;
        }

        string filedName = strArray[strArray.Length - 1];

        for (int i = 0; i < strArray.Length - 1; i++)
        {
            string str = strArray[i];
            string comName;
            if (m_PrefixesDict.TryGetValue(str, out comName))
            {
                filedNames.Add($"{str}_{filedName}");
                componentTypeNames.Add(comName);
            }
            else
            {
                Debug.LogError($"{target.name}的命名中{str}不存在对应的组件类型，绑定失败");
                return false;
            }
        }
        return true;
    }
}
