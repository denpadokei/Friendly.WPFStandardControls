﻿using System;
using Codeer.Friendly.Windows;
using RM.Friendly.WPFStandardControls.Properties;

namespace RM.Friendly.WPFStandardControls.Inside
{
    /// <summary>
    /// ローカライズ済みリソース。
    /// </summary>
    [Serializable]
    class ResourcesLocal
    {
        static internal ResourcesLocal Instance;

        internal string DataGridErrorNotTextBoxCell;
        internal string DataGridErrorNotComboBoxCell;
        internal string DataGridErrorNotCheckBoxCell;
        internal string DataGridErrorHasNotTextProperty;

        /// <summary>
        /// 初期化。
        /// </summary>
        /// <param name="app">アプリケーション操作クラス。</param>
        internal static void Initialize(WindowsAppFriend app)
        {
            Instance = new ResourcesLocal();
            Instance.Initialize();
            app[typeof(ResourcesLocal), "Instance"](Instance);
        }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        void Initialize()
        {
            DataGridErrorNotTextBoxCell = Resources.DataGridErrorNotTextBoxCell;
            DataGridErrorNotComboBoxCell = Resources.DataGridErrorNotComboBoxCell;
            DataGridErrorNotCheckBoxCell = Resources.DataGridErrorNotCheckBoxCell;
            DataGridErrorHasNotTextProperty = Resources.DataGridErrorHasNotTextProperty;
        }
    }
}
