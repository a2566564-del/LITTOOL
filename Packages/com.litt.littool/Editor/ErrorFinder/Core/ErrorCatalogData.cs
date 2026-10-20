#pragma warning disable 0649 // fields are filled by JsonUtility
using System;

namespace Littool.ErrorFinder
{
    // The error list shipped with LITTOOL (ErrorCatalog.json), exported from the error-list workbook.
    // Plain serializable classes so both UnityEngine.JsonUtility and the test harness can read them.
    // Nothing in this folder (Core) uses Unity APIs.

    [Serializable]
    internal sealed class ErrorCatalogData
    {
        public string version;
        public string date;
        public double threshold = 0.65;
        public int minSimLen = 30;
        public int cjkWeight = 2;
        public CatalogRow[] rows;
        /// <summary>Sentences used for the template step (the list's own variants, then the tools' translations).</summary>
        public CatalogDoc[] docs;
        /// <summary>Sentences of the similarity index (templates + translations kept only to tell MA codes apart).</summary>
        public CatalogDoc[] index;
        public CatalogIgnore[] ignore;
        public CatalogTool[] tools;
        public CatalogCallback[] callbacks;
        public string[] unityUi;
        public CatalogAssetFolder[] assetFolders;
        public CatalogRule[] rules;
        public CatalogToolGroup[] toolGroups;
        public CatalogGroup[] groups;
        public CatalogChoice[] choices;
    }

    [Serializable]
    internal sealed class CatalogRow
    {
        public int no;
        public int group;
        public string groupName;
        /// <summary>업로드 차단 · 플레이 차단 · 플레이·업로드 차단 · 기능 깨짐 · 프로젝트 차단</summary>
        public string sev;
        public string when;
        public string where;
        public string msg;
        public string what;
        public string cause;
        public string fix;
        /// <summary>SDK Auto Fix · 툴로 수정 가능 · 툴로 위치 안내 · 안내만</summary>
        public string auto;
        public string basis;
        public string source;
        /// <summary>결과 메시지: 예 · 감쌈 · 아니오</summary>
        public string result;
        public string kw;
        public string[] keywords;
        public string[] maCodes;
        public bool codeIsKeyword;
        /// <summary>normal · keywordOnly · locationOnly · noLog</summary>
        public string matchMode;
        public string note;
    }

    [Serializable]
    internal sealed class CatalogDoc
    {
        public int row;
        public string text;
    }

    [Serializable]
    internal sealed class CatalogIgnore
    {
        public string id;
        public string message;
        public string[] patterns;
        public string tool;
        public string kind;
        public string why;
        public string source;
    }

    [Serializable]
    internal sealed class CatalogTool
    {
        public string tool;
        public string[] prefixes;
        public string[] packages;
    }

    [Serializable]
    internal sealed class CatalogCallback
    {
        public string tool;
        public string[] names;
        public string source;
    }

    [Serializable]
    internal sealed class CatalogAssetFolder
    {
        public string folder;
        public string tool;
    }

    [Serializable]
    internal sealed class CatalogRule
    {
        public string id;
        public string when;
        public string where;
        public string judge;
        public string guide;
        public string example;
    }

    [Serializable]
    internal sealed class CatalogToolGroup
    {
        public string tool;
        public int[] groups;
    }

    [Serializable]
    internal sealed class CatalogGroup
    {
        public int no;
        public string name;
        public string desc;
    }

    [Serializable]
    internal sealed class CatalogChoice
    {
        public int row;
        public string question;
        public string hint;
        public CatalogChoiceOption[] options;
    }

    [Serializable]
    internal sealed class CatalogChoiceOption
    {
        public string label;
        public string[] steps;
        public string note;
    }
}
