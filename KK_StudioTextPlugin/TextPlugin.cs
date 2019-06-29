﻿using System;
using System.Linq;
using BepInEx.Logging;
using Studio;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace KK_StudioTextPlugin {
    static class TextPlugin {
        private static Material font3DMaterial;
        internal static void Start() {
            //載入font3DMaterial，為了解決UI文字穿透其他物品的問題
            //因為文字無法編輯Shader，只能做Material來用
            if (AssetBundle.LoadFromMemory(Properties.Resources.text) is AssetBundle assetBundle) {
                font3DMaterial = assetBundle.LoadAsset<Material>("Font3DMaterial");
                font3DMaterial.color = Color.white;
                assetBundle.Unload(false);
            } else {
                Logger.Log(LogLevel.Error, "[KK_STP] Load assetBundle faild");
            }
        }

        /// <summary>
        /// 更改選取項目的字型
        /// </summary>
        /// <param name="fontName">字型名稱</param>
        public static void ChangeFont(string fontName) {
            OCIFolder[] folderArray = (from v in Singleton<GuideObjectManager>.Instance.selectObjectKey
                                       select Studio.Studio.GetCtrlInfo(v) as OCIFolder into v
                                       where v != null
                                       select v).ToArray();
            foreach (var oCIFolder in folderArray) {
                SetFont(oCIFolder, fontName);
                MakeAndSetConfigStructure(oCIFolder.treeNodeObject, Config.Font);
            }
        }

        /// <summary>
        /// 更改選取項目的字體大小
        /// </summary>
        /// <param name="size">字體大小</param>
        public static void ChangeCharacterSize(float size) {
            OCIFolder[] folderArray = (from v in Singleton<GuideObjectManager>.Instance.selectObjectKey
                                       select Studio.Studio.GetCtrlInfo(v) as OCIFolder into v
                                       where v != null
                                       select v).ToArray();
            foreach (var oCIFolder in folderArray) {
                SetCharacterSize(oCIFolder, size);
                MakeAndSetConfigStructure(oCIFolder.treeNodeObject, Config.FontSize);
            }
        }

        /// <summary>
        /// 在給定的OCIFolder GameObject下添加TextMesh
        /// </summary>
        /// <param name="folder">要添加TextMesh的OCIFolder</param>
        /// <param name="text">預設文字</param>
        /// <returns></returns>
        public static TextMesh MakeTextObj(OCIFolder folder, string text) {
            folder.objectItem.layer = 10;
            GameObject go = new GameObject();
            go.transform.SetParent(folder.objectItem.transform);
            go.layer = 10;
            go.transform.localPosition = Vector3.zero;
            TextMesh t = go.AddComponent<TextMesh>();
            t.fontSize = 500;
            t.anchor = TextAnchor.MiddleCenter;
            t.text = text;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            SetFont(folder);
            SetCharacterSize(folder);
            Patches.DrawConfigPanel();

            Logger.Log(LogLevel.Info, "[KK_STP] Create Text");
            return t;
        }

        /// <summary>
        /// 設定字型，預設為MS Gothic
        /// </summary>
        /// <param name="folder">對象OCIFolder</param>
        /// <param name="fontName">字型名稱</param>
        public static void SetFont(OCIFolder folder, string fontName = "MS Gothic") {
            if (!CheckFontInOS(fontName)) {
                if (!String.Equals(fontName, "MS Gothic")) {
                    Logger.Log(LogLevel.Message, "[KK_STP] Fallback to MS Gothic");
                    fontName = "MS Gothic";
                } else {
                    Logger.Log(LogLevel.Message, "[KK_STP] Fallback to Arial");
                    fontName = "Arial";
                }
            }
            folder.objectItem.GetComponentInChildren<TextMesh>().font = Font.CreateDynamicFontFromOSFont(fontName, 200);
            folder.objectItem.GetComponentInChildren<MeshRenderer>().material = font3DMaterial;
            folder.objectItem.GetComponentInChildren<MeshRenderer>().material.SetTexture("_MainTex", folder.objectItem.GetComponentInChildren<TextMesh>().font.material.mainTexture);
            folder.objectItem.GetComponentInChildren<MeshRenderer>().material.EnableKeyword("_NORMALMAP");
            return;
        }

        /// <summary>
        /// 設定字體大小，單位放大一百倍
        /// </summary>
        /// <param name="folder">對象OCIFolder</param>
        /// <param name="size">字體大小</param>
        public static void SetCharacterSize(OCIFolder folder, float size = 1) {
            folder.objectItem.GetComponentInChildren<TextMesh>().characterSize = 0.002f * size;
            return;
        }

        /// <summary>
        /// 檢查OS中是否有安裝給定字型
        /// </summary>
        /// <param name="fontName">字型名稱</param>
        /// <returns></returns>
        public static bool CheckFontInOS(string fontName) {
            var i = Array.IndexOf(Font.GetOSInstalledFontNames(), fontName);
            if (i >= 0) {
                return true;
            }
            Logger.Log(LogLevel.Message, "[KK_STP] Missing font: " + fontName);
            return false;
        }

        /// <summary>
        /// 字型設定項目
        /// </summary>
        public enum Config {
            All = 0,
            Font,
            FontSize,
            Color
        }

        /// <summary>
        /// 創建設定資料夾結構，或是變更設定中的特定項目
        /// </summary>
        /// <param name="textTreeNodeObject">要變更的OCIFolder.treeNodeObject</param>
        /// <param name="config">要設定的項目</param>
        public static void MakeAndSetConfigStructure(TreeNodeObject textTreeNodeObject, Config config = Config.All) {
            OCIFolder textOCIFolder = Studio.Studio.GetCtrlInfo(textTreeNodeObject) as OCIFolder;
            TextMesh t = textOCIFolder.objectItem.GetComponentInChildren<TextMesh>();
            MeshRenderer m = textOCIFolder.objectItem.GetComponentInChildren<MeshRenderer>();
            TreeNodeCtrl treeNodeCtrl = Singleton<Studio.Studio>.Instance.treeNodeCtrl;

            TreeNodeObject nConfig = doMain(Patches.TextConfigPrefix, "", textTreeNodeObject);
            if (config == Config.Font || config == Config.All)
                doMain(Patches.TextConfigFontPrefix, t.font.name, nConfig);
            if (config == Config.FontSize || config == Config.All)
                doMain(Patches.TextConfigFontSizePrefix, (t.characterSize*500).ToString(), nConfig);
            if (config == Config.Color || config == Config.All)
                doMain(Patches.TextConfigColorPrefix, m.material.color.ToString(), nConfig);

            TreeNodeObject doMain(string prefix, string value, TreeNodeObject nRoot) {
                TreeNodeObject node = nRoot.child?.Where((x) =>
                    Studio.Studio.GetCtrlInfo(x).objectInfo.kind == 3 &&
                    (Studio.Studio.GetCtrlInfo(x) is OCIFolder y) &&
                    y.name.Contains(prefix)
                ).FirstOrDefault();
                OCIFolder folder;
                if (null == node) {
                    //沒有找到就創建
                    folder = AddObjectFolder.Add();
                    treeNodeCtrl.SetParent(folder.treeNodeObject, nRoot);
                    folder.objectInfo.changeAmount.Reset();
                    node = folder.treeNodeObject;
                } else {
                    folder = Studio.Studio.GetCtrlInfo(node) as OCIFolder;
                }
                folder.name = folder.objectItem.name = prefix + value;
                return node;
            }
        }

        /// <summary>
        /// 讀取資料夾結構內的設定值
        /// </summary>
        /// <param name="textTreeNodeObject">要讀取的OCIFolder.treeNodeObject</param>
        /// <param name="config">要讀取的項目</param>
        /// <returns></returns>
        public static string GetConfig(TreeNodeObject textTreeNodeObject, Config config) {
            if (null == textTreeNodeObject) {
                textTreeNodeObject = (from v in Singleton<GuideObjectManager>.Instance.selectObjectKey
                                      select Studio.Studio.GetCtrlInfo(v) as OCIFolder into v
                                      where v != null
                                      select v.treeNodeObject).FirstOrDefault();
            }
            OCIFolder textOCIFolder = Studio.Studio.GetCtrlInfo(textTreeNodeObject) as OCIFolder;
            TextMesh t = textOCIFolder.objectItem.GetComponentInChildren<TextMesh>();
            MeshRenderer m = textOCIFolder.objectItem.GetComponentInChildren<MeshRenderer>();
            TreeNodeObject GetChildNode(string prefix, TreeNodeObject nRoot) {
                return nRoot.child?.Where((x) =>
                    Studio.Studio.GetCtrlInfo(x).objectInfo.kind == 3 &&
                    (Studio.Studio.GetCtrlInfo(x) is OCIFolder y) &&
                    y.name.Contains(prefix)
                ).FirstOrDefault();
            }

            TreeNodeObject nConfig = GetChildNode(Patches.TextConfigPrefix, textTreeNodeObject);
            string GetValue(string prefix) {
                OCIFolder f = Studio.Studio.GetCtrlInfo(GetChildNode(prefix, nConfig)) as OCIFolder;
                return f.name.Replace(prefix, "");
            }

            switch (config) {
                case Config.Font:
                    return GetValue(Patches.TextConfigFontPrefix);
                case Config.FontSize:
                    return GetValue(Patches.TextConfigFontSizePrefix).ToString();
                case Config.Color:
                    return GetValue(Patches.TextConfigColorPrefix);
                default:
                    return "";
            }
        }
    }
}
