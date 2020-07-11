﻿using Codeer.TestAssistant.GeneratorToolKit;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RM.Friendly.WPFStandardControls.Generator.CreateDriver
{
    public class DriverDesigner : IDriverDesigner
    {
        const string Indent = "    ";
        const string TodoComment = "// TODO It is not the best way to identify. Please change to a better method.";
        const string WindowsAppFriendTypeFullName = "Codeer.Friendly.Windows.WindowsAppFriend";
        const string AttachByTypeFullName = "Type Full Name";
        const string AttachByWindowText = "Window Text";
        const string AttachVariableWindowText = "VariableWindowText";
        const string AttachCustom = "Custom";

        public int Priority { get; }

        public bool CanDesign(object obj) => obj is Window || obj is UserControl || obj is Page;

        public string CreateDriverClassName(object coreObj)
        {
            var driverTypeNameManager = new DriverTypeNameManager(DriverCreatorAdapter.SelectedNamespace, DriverCreatorAdapter.TypeFullNameAndWindowDriver, DriverCreatorAdapter.TypeFullNameAndUserControlDriver);
            return driverTypeNameManager.MakeDriverType(coreObj, out var _);
        }

        public string[] GetAttachExtensionClassCandidates(object obj)
        {
            var candidates = new List<string>();
            var parent = VisualTreeHelper.GetParent((DependencyObject)obj);
            while (parent != null)
            {
                var driver = DriverCreatorUtils.GetDriverTypeFullName(parent, new Dictionary<string, ControlDriverInfo>(),
                                                                    DriverCreatorAdapter.TypeFullNameAndUserControlDriver,
                                                                    DriverCreatorAdapter.TypeFullNameAndWindowDriver, out var _);
                if (!string.IsNullOrEmpty(driver))
                {
                    candidates.Add(driver);
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            candidates.Add(WindowsAppFriendTypeFullName);
            return candidates.ToArray();
        }

        public string[] GetAttachMethodCandidates(object obj)
        {
            var candidates = new List<string>();
            candidates.Add(AttachByTypeFullName);
            if (obj is Window)
            {
                candidates.Add(AttachByWindowText);
                candidates.Add(AttachVariableWindowText);
            }
            candidates.Add(AttachCustom);
            return candidates.ToArray();
        }

        public DriverIdentifyInfo[] GetIdentifyingCandidates(object root, object element)
        {
            var rootCtrl = root as DependencyObject;
            var elementCtrl = element as DependencyObject;
            if (rootCtrl == null || elementCtrl == null) return new DriverIdentifyInfo[0];

            using (var dom = CodeDomProvider.CreateProvider("CSharp"))
            {
                var infos = GetIdentifyingCandidatesCore(dom, rootCtrl, elementCtrl);
                if (infos != null) return infos;
            }

            return new DriverIdentifyInfo[0];
        }

        public void GenerateCode(object targetControl, DriverDesignInfo info)
        {
            var code = GenerateCodeCore((Control)targetControl, info);
            var fileName = $"{info.ClassName}.cs";
            DriverCreatorAdapter.AddCode(fileName, code, targetControl);

            //行選択でのツリーとの連動用
            foreach (var e in info.Properties)
            {
                DriverCreatorAdapter.AddCodeLineSelectInfo(fileName, e.Identify, e.Element);
            }
        }

        static void GetMembers(DriverDesignInfo info, out List<string> usings, out List<string> members)
        {
            usings = new List<string>();
            members = new List<string>();
            var fileName = $"{info.ClassName}.cs";
            foreach (var e in info.Properties)
            {
                var typeName = DriverCreatorUtils.GetTypeName(e.TypeFullName);
                var nameSpace = DriverCreatorUtils.GetTypeNamespace(e.TypeFullName);
                var todo = (e.IsPerfect.HasValue && !e.IsPerfect.Value) ? WPFDriverCreator.TodoComment : string.Empty;
                members.Add($"public {typeName} {e.Name} => {e.Identify}; {todo}");
                if (!usings.Contains(nameSpace)) usings.Add(nameSpace);
                foreach (var x in e.ExtensionUsingNamespaces)
                {
                    if (!usings.Contains(x)) usings.Add(x);
                }
            }
        }

        string GenerateCodeCore(Control targetControl, DriverDesignInfo info)
        {
            //クラス定義部分
            var classDefine = GenerateClassDefine(targetControl, info, out var memberUsings);

            //拡張メソッド部分
            var extentionsDefine = GenerateExtensions(targetControl, info, out var extensionUsings);

            //using
            var usings = new List<string>();
            DistinctAddRange(new[]
                    {
                        "Codeer.TestAssistant.GeneratorToolKit",
                        "Codeer.Friendly.Windows.Grasp",
                        "Codeer.Friendly.Windows",
                        "Codeer.Friendly.Dynamic",
                        "Codeer.Friendly",
                        "System.Linq"
                    }, usings);
            DistinctAddRange(memberUsings, usings);
            DistinctAddRange(extensionUsings, usings);
            usings.Sort();

            //コード作成
            var code = new List<string>();
            foreach (var e in usings)
            {
                code.Add($"using {e};");
            }
            code.Add(string.Empty);
            code.Add($"namespace {DriverCreatorAdapter.SelectedNamespace}");
            code.Add("{");
            code.AddRange(classDefine);
            code.AddRange(extentionsDefine);
            code.Add("}");
            return string.Join(Environment.NewLine, code.ToArray());
        }

        static List<string> GenerateClassDefine(object targetControl, DriverDesignInfo info, out List<string> usings)
        {
            GetMembers(info, out usings, out var members);

            var code = new List<string>();

            var attr = (targetControl is Window) ? "WindowDriver" : "UserControlDriver";
            code.Add($"{Indent}[{attr}(TypeFullName = \"{targetControl.GetType().FullName}\")]");
            code.Add($"{Indent}public class {info.ClassName}");
            code.Add($"{Indent}{{");
            code.Add($"{Indent}{Indent}public WindowControl Core {{ get; }}");
            foreach (var e in members)
            {
                code.Add($"{Indent}{Indent}{e}");
            }
            code.Add(string.Empty);
            code.Add($"{Indent}{Indent}public {info.ClassName}(WindowControl core)");
            code.Add($"{Indent}{Indent}{{");
            code.Add($"{Indent}{Indent}{Indent}Core = core;");
            code.Add($"{Indent}{Indent}}}");

            code.Add(string.Empty);
            code.Add($"{Indent}{Indent}public {info.ClassName}(AppVar core)");
            code.Add($"{Indent}{Indent}{{");
            code.Add($"{Indent}{Indent}{Indent}Core = new WindowControl(core);");
            code.Add($"{Indent}{Indent}}}");
            code.Add($"{Indent}}}");

            return code;
        }

        static List<string> GenerateExtensions(Control targetControl, DriverDesignInfo info, out List<string> usings)
        {
            var code = new List<string>();
            usings = new List<string>();

            if (!info.CreateAttachCode) return code;

            code.Add(string.Empty);
            code.Add($"{Indent}public static class {info.ClassName}Extensions");
            code.Add($"{Indent}{{");

            var funcName = GetFuncName(info.ClassName);

            //WindowsAppFriendにアタッチする場合
            if (info.AttachExtensionClass == WindowsAppFriendTypeFullName)
            {
                if (targetControl is Window window)
                {
                    if (info.AttachMethod == AttachCustom)
                    {
                        code.Add($"{Indent}{Indent}[WindowDriverIdentify(CustomMethod = \"TryGet\")]");
                        code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this WindowsAppFriend app, T identifier)");
                        code.Add($"{Indent}{Indent}{{");
                        code.Add($"{Indent}{Indent}{Indent}//TODO");
                        code.Add($"{Indent}{Indent}}}");
                        code.Add(string.Empty);
                        code.Add($"{Indent}{Indent}public static bool TryGet(WindowControl window, out T identifier)");
                        code.Add($"{Indent}{Indent}{{");
                        code.Add($"{Indent}{Indent}{Indent}//TODO");
                        code.Add($"{Indent}{Indent}}}");
                    }
                    else if (info.AttachMethod == AttachVariableWindowText)
                    {
                        code.Add($"{Indent}{Indent}[WindowDriverIdentify(CustomMethod = \"TryGet\")]");
                        code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this WindowsAppFriend app, string text)");
                        code.Add($"{Indent}{Indent}{Indent}=> app.WaitForIdentifyFromWindowText(\"{window.Title}\").Dynamic();");
                        code.Add(string.Empty);
                        code.Add($"{Indent}{Indent}public static bool TryGet(WindowControl window, out string text)");
                        code.Add($"{Indent}{Indent}{{");
                        code.Add($"{Indent}{Indent}{Indent}text = window.GetWindowText();");
                        code.Add($"{Indent}{Indent}{Indent}return window.TypeFullName == \"{targetControl.GetType().FullName}\";");
                        code.Add($"{Indent}{Indent}}}");
                    }
                    else
                    {
                        if (info.ManyExists)
                        {
                            if (info.AttachMethod == AttachByTypeFullName)
                            {
                                code.Add($"{Indent}{Indent}[WindowDriverIdentify(CustomMethod = \"TryGet\")]");
                                code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this WindowsAppFriend app, int index)");
                                code.Add($"{Indent}{Indent}{Indent}=> app.GetFromTypeFullName(\"{targetControl.GetType().FullName}\")[index].Dynamic();");
                                code.Add(string.Empty);
                                code.Add($"{Indent}{Indent}public static bool TryGet(WindowControl window, out int index)");
                                code.Add($"{Indent}{Indent}{{");
                                code.Add($"{Indent}{Indent}{Indent}index = window.App.GetFromTypeFullName(\"{targetControl.GetType().FullName}\").Select(e => e.Handle).ToList().IndexOf(window.Handle);");
                                code.Add($"{Indent}{Indent}{Indent}return index != -1;");
                                code.Add($"{Indent}{Indent}}}");
                            }
                            else
                            {
                                code.Add($"{Indent}{Indent}[WindowDriverIdentify(CustomMethod = \"TryGet\")]");
                                code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this WindowsAppFriend app, int index)");
                                code.Add($"{Indent}{Indent}{Indent}=> app.GetFromWindowText(\"{window.Title}\")[index].Dynamic();");
                                code.Add(string.Empty);
                                code.Add($"{Indent}{Indent}public static bool TryGet(WindowControl window, out int index)");
                                code.Add($"{Indent}{Indent}{{");
                                code.Add($"{Indent}{Indent}{Indent}index = window.App.GetFromWindowText(\"{window.Title}\").Select(e => e.Handle).ToList().IndexOf(window.Handle);");
                                code.Add($"{Indent}{Indent}{Indent}return index != -1;");
                                code.Add($"{Indent}{Indent}}}");
                            }
                        }
                        else
                        {
                            if (info.AttachMethod == AttachByTypeFullName)
                            {
                                code.Add($"{Indent}{Indent}[WindowDriverIdentify(TypeFullName = \"{targetControl.GetType().FullName}\")]");
                                code.Add($"{Indent}{Indent}public static {info.ClassName} {GetFuncName(info.ClassName)}(this WindowsAppFriend app)");
                                code.Add($"{Indent}{Indent}{Indent}=> app.WaitForIdentifyFromTypeFullName(\"{targetControl.GetType().FullName}\").Dynamic();");
                            }
                            else
                            {
                                code.Add($"{Indent}{Indent}[WindowDriverIdentify(WindowText = \"{window.Title}\")]");
                                code.Add($"{Indent}{Indent}public static {info.ClassName} {GetFuncName(info.ClassName)}(this WindowsAppFriend app)");
                                code.Add($"{Indent}{Indent}{Indent}=> app.WaitForIdentifyFromWindowText(\"{window.Title}\").Dynamic();");
                            }
                        }
                    }
                }
                //UserControl
                else
                {
                    if (info.AttachMethod == AttachCustom)
                    {
                        code.Add($"{Indent}{Indent}[UserControlDriverIdentify(CustomMethod = \"TryGet\")]");
                        code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this WindowsAppFriend app, T identifier)");
                        code.Add($"{Indent}{Indent}{{");
                        code.Add($"{Indent}{Indent}{Indent}//TODO");
                        code.Add($"{Indent}{Indent}}}");
                        code.Add(string.Empty);
                        code.Add($"{Indent}{Indent}public static void TryGet(this WindowsAppFriend app, out T[] identifiers)");
                        code.Add($"{Indent}{Indent}{{");
                        code.Add($"{Indent}{Indent}{Indent}//TODO");
                        code.Add($"{Indent}{Indent}}}");
                    }
                    else
                    {
                        if (info.ManyExists)
                        {
                            if (info.AttachMethod == AttachByTypeFullName)
                            {
                                code.Add($"{Indent}{Indent}[UserControlDriverIdentify(CustomMethod = \"TryGet\")]");
                                code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this WindowsAppFriend app, int index)");
                                code.Add($"{Indent}{Indent}{Indent}=> app.GetTopLevelWindows().SelectMany(e => e.GetFromTypeFullName(\"{targetControl.GetType().FullName}\")).ToArray()[index].Dynamic();");
                                code.Add(string.Empty);
                                code.Add($"{Indent}{Indent}public static void TryGet(this WindowsAppFriend app, out int[] indices)");
                                code.Add($"{Indent}{Indent}{Indent}=> indices = Enumerable.Range(0, app.GetTopLevelWindows().Sum(e => e.GetFromTypeFullName(\"{targetControl.GetType().FullName}\").Length)).ToArray();");
                            }
                        }
                        else
                        {
                            if (info.AttachMethod == AttachByTypeFullName)
                            {
                                code.Add($"{Indent}{Indent}[UserControlDriverIdentify]");
                                code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this WindowsAppFriend app)");
                                code.Add($"{Indent}{Indent}{Indent}=> app.GetTopLevelWindows().SelectMany(e => e.GetFromTypeFullName(\"{targetControl.GetType().FullName}\")).SingleOrDefault()?.Dynamic();");
                            }
                        }
                    }
                }
            }
            //ドライバへのアタッチ
            else
            {
                SeparateNameSpaceAndTypeName(info.AttachExtensionClass, out var ns, out var parentDriver);
                if (!string.IsNullOrEmpty(ns))
                {
                    usings.Add(ns);
                }

                if (info.AttachMethod == AttachCustom)
                {
                    code.Add($"{Indent}{Indent}[UserControlDriverIdentify(CustomMethod = \"TryGet\")]");
                    code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this {parentDriver} parent, T identifier)");
                    code.Add($"{Indent}{Indent}{{");
                    code.Add($"{Indent}{Indent}{Indent}//TODO");
                    code.Add($"{Indent}{Indent}}}");
                    code.Add(string.Empty);
                    code.Add($"{Indent}{Indent}public static void TryGet(this {parentDriver} parent, out T identifier)");
                    code.Add($"{Indent}{Indent}{{");
                    code.Add($"{Indent}{Indent}{Indent}//TODO");
                    code.Add($"{Indent}{Indent}}}");
                }
                else
                {
                    if (info.ManyExists)
                    {
                        if (info.AttachMethod == AttachByTypeFullName)
                        {
                            code.Add($"{Indent}{Indent}[UserControlDriverIdentify(CustomMethod = \"TryGet\")]");
                            code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this {parentDriver} parent, int index)");
                            code.Add($"{Indent}{Indent}{Indent}=> parent.Core.GetFromTypeFullName(\"{targetControl.GetType().FullName}\")[index].Dynamic();");
                            code.Add(string.Empty);
                            code.Add($"{Indent}{Indent}public static void TryGet(this {parentDriver} parent, out int[] indices)");
                            code.Add($"{Indent}{Indent}{Indent}=> indices = Enumerable.Range(0, parent.Core.GetFromTypeFullName(\"{targetControl.GetType().FullName}\").Length).ToArray();");
                        }
                    }
                    else
                    {
                        if (info.AttachMethod == AttachByTypeFullName)
                        {
                            code.Add($"{Indent}{Indent}[UserControlDriverIdentify]");
                            code.Add($"{Indent}{Indent}public static {info.ClassName} {funcName}(this {parentDriver} parent)");
                            code.Add($"{Indent}{Indent}{Indent}=> parent.Core.GetFromTypeFullName(\"{targetControl.GetType().FullName}\").SingleOrDefault()?.Dynamic();");
                        }
                    }
                }
            }
            code.Add($"{Indent}}}");

            return code;
        }

         static void SeparateNameSpaceAndTypeName(string attachExtensionClass, out string ns, out string parentDriver)
        {
            ns = string.Empty;
            parentDriver = attachExtensionClass;

            var sp = attachExtensionClass.Split('.');
            if (sp.Length < 2) return;

            parentDriver = sp[sp.Length - 1];
            var nsArray = new string[sp.Length - 1];
            Array.Copy(sp, nsArray, nsArray.Length);
            ns = string.Join(".", nsArray);
        }

        static string GetFuncName(string driverClassName)
        {
            var index = driverClassName.IndexOf(DriverCreatorUtils.Suffix);
            if (0 < index && index == driverClassName.Length - DriverCreatorUtils.Suffix.Length) return "Attach" + driverClassName;

            return $"Attach{driverClassName.Substring(0, driverClassName.Length - DriverCreatorUtils.Suffix.Length)}";
        }

        static void DistinctAddRange(IEnumerable<string> src, List<string> dst)
        {
            foreach (var e in src)
            {
                if (!dst.Contains(e)) dst.Add(e);
            }
        }

        DriverIdentifyInfo[] GetIdentifyingCandidatesCore(CodeDomProvider dom, DependencyObject rootCtrl, DependencyObject elementCtrl)
        {
            var creator = new WPFDriverCreator(dom);

            var ancestor = new List<DependencyObject>();
            var current = VisualTreeHelper.GetParent(elementCtrl);
            while (current != null)
            {
                if (CanDesign(current))
                {
                    ancestor.Add(current);
                }
                if (ReferenceEquals(current, rootCtrl)) break;
                current = VisualTreeHelper.GetParent(current);
            }

            //Fieldでたどることができる範囲を取得
            var target = elementCtrl;
            var accessPaths = new List<string>();
            var bindingExpressionCache = new BindingExpressionCache();
            var isPerfect = true;
            string name = string.Empty;
            var usings = new List<string>();
            var needDynamic = true;
            foreach (var e in ancestor)
            {
                //直接のフィールドに持っているか？
                var path = GetAccessPath(e, target);
                if (!string.IsNullOrEmpty(path))
                {
                    if (target == elementCtrl)
                    {
                        var sp = path.Split('.');
                        name = sp.Length == 0 ? string.Empty : sp[sp.Length - 1];
                    }
                    accessPaths.Insert(0, path);
                    target = e;
                    needDynamic = true;
                    continue;
                }

                //Treeから検索
                var logicalForGetter = WPFUtility.GetLogicalTreeDescendants(e, false, false, 0);
                var visualForGetter = WPFUtility.GetVisualTreeDescendants(e, false, false, 0);
                path = creator.MakeCodeGetFromTree(string.Empty, logicalForGetter, visualForGetter, target, bindingExpressionCache, usings, out var nogood);
                if (!needDynamic)
                {
                    var toDynamic = ".Dynamic()";
                    var index = path.LastIndexOf(toDynamic);
                    if (index == path.Length - toDynamic.Length)
                    {
                        path = path.Substring(0, path.Length - toDynamic.Length);
                    }
                }

                if (nogood) isPerfect = false;
                if (!string.IsNullOrEmpty(path))
                {
                    accessPaths.Insert(0, path);
                    target = e;
                    needDynamic = false;
                    continue;
                }

                break;
            }

            if (target != rootCtrl) return null;

            if (string.IsNullOrEmpty(name))
            {
                var names = new List<string>();
                var customNameGenerator = new DriverElementNameGeneratorAdaptor(dom);
                name = customNameGenerator.MakeDriverPropName(elementCtrl, string.Empty, names);
            }

            if (needDynamic)
            {
                accessPaths.Insert(0, "Dynamic()");
            }
            var accessPath = string.Join(".", accessPaths.ToArray());
            return new[]
            {
                new DriverIdentifyInfo
                {
                    IsPerfect = isPerfect,
                    Identify = "Core." + accessPath,
                    DefaultName = name,
                    ExtensionUsingNamespaces = usings.ToArray()
                }
            };
        }

        static string GetAccessPath(DependencyObject parent, DependencyObject target)
        {
            foreach (var e in parent.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (ReferenceEquals(e.GetValue(parent), target)) return e.Name;
            }
            return string.Empty;
        }
    }
}
