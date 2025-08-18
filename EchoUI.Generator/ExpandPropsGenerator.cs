using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace EchoUI.Generator
{
    [Generator(LanguageNames.CSharp)]
    public class ExpandPropsGenerator : IIncrementalGenerator
    {
        private const string ElementAttributeMetadataName = "EchoUI.Core.ElementAttribute"; // 若有命名空间，请改成完全限定名
        private const string PropsSimpleName = "Props";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1) 找到标记了 [Element] 的方法
            var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: ElementAttributeMetadataName,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var method = (IMethodSymbol)ctx.TargetSymbol;
                    var attr = ctx.Attributes.FirstOrDefault();
                    return (method, attr);
                });

            // 2) 过滤 Props 或子类
            var targets = candidates.Where(t =>
            {
                var method = t.method;
                if (method.Parameters.Length == 0) return false;
                return IsPropsOrDerived(method.Parameters[0].Type);
            });

            // 3) 合并编译对象
            var withCompilation = context.CompilationProvider.Combine(targets.Collect());

            // 4) 输出
            context.RegisterSourceOutput(withCompilation, (spc, pair) =>
            {
                var compilation = pair.Left;
                var items = pair.Right.AsEnumerable().Distinct(SymbolTupleComparer.Instance);

                foreach (var tuple in items)
                {
                    var method = tuple.method;
                    var attr = tuple.attr;
                    try
                    {
                        EmitForMethod(spc, compilation, method, attr);
                    }
                    catch (Exception ex)
                    {
                        var hint = $"ElementGen_Error_{method.ContainingType.Name}_{method.Name}";
                        spc.AddSource(hint, $"// Error generating for {method.ToDisplayString()}: {ex}");
                    }
                }
            });
        }

        private static bool IsPropsOrDerived(ITypeSymbol t)
        {
            // 名称为 Props，或其任一基类名为 Props
            for (ITypeSymbol? cur = t; cur is not null; cur = cur.BaseType)
            {
                if (cur.Name == PropsSimpleName) return true;
            }
            return false;
        }

        private static void EmitForMethod(SourceProductionContext spc, Compilation compilation, IMethodSymbol method, AttributeData? attr)
        {
            var container = method.ContainingType;
            if (!container.DeclaringSyntaxReferences.Any(s =>
                    (s.GetSyntax() as TypeDeclarationSyntax)?.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) == true))
            {
                var descriptor = new DiagnosticDescriptor(
                    id: "EG001",
                    title: "容器类型必须是 partial",
                    messageFormat: "方法 {0} 的所在类型 {1} 必须标记为 partial 才能生成重载。",
                    category: "ElementGen",
                    DiagnosticSeverity.Warning, isEnabledByDefault: true);
                spc.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None,
                    method.Name, container.ToDisplayString()));
                return;
            }

            var param = method.Parameters[0];
            var propsType = param.Type;
            var props = EnumerateProps(propsType).ToList();
            if (props.Count == 0) return;

            // 处理 DefaultProperty
            var defaultPropName = GetDefaultPropertyName(attr);
            IPropertySymbol? defaultProp = null;
            if (defaultPropName is not null)
            {
                defaultProp = props.FirstOrDefault(p => p.Name == defaultPropName);
                if (defaultProp != null)
                {
                    props.Remove(defaultProp);
                }
                else
                {
                    var descriptor = new DiagnosticDescriptor(
                        id: "EG002",
                        title: "DefaultProperty 未匹配到",
                        messageFormat: "在 {0} 的 Props 类型 {1} 中找不到 DefaultProperty '{2}'。",
                        category: "ElementGen",
                        DiagnosticSeverity.Info, isEnabledByDefault: true);
                    spc.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None,
                        method.Name, propsType.ToDisplayString(), defaultPropName));
                }
            }

            // 按可空性排序
            var sortedProps = props
                .OrderBy(p => p.Type.NullableAnnotation == NullableAnnotation.Annotated ? 1 : 0)
                .ToList();

            if (defaultProp != null)
                sortedProps.Insert(0, defaultProp);

            props = sortedProps;

            var ns = container.ContainingNamespace.IsGlobalNamespace
                ? null
                : container.ContainingNamespace.ToDisplayString();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");

            if (ns is not null)
            {
                sb.Append("namespace ").Append(ns).AppendLine();
                sb.AppendLine("{");
            }

            sb.Append("partial ");
            if (container.IsStatic) sb.Append("static ");
            sb.Append(container.TypeKind switch
            {
                TypeKind.Class => "class ",
                TypeKind.Struct => "struct ",
                _ => "class "
            });
            sb.Append(container.Name);
            sb.AppendLine();
            sb.AppendLine("{");

            var attrText = attr is null ? null : NormalizeElementAttribute(attr, defaultPropName);
            if (attrText is not null)
                sb.AppendLine($"    {attrText}");

            sb.Append("    public static ");
            sb.Append(method.ReturnsVoid ? "void " : method.ReturnType.ToDisplayString() + " ");
            sb.Append(method.Name).Append("(");

            var parametersWithDefaults = new List<(IPropertySymbol prop, string? compileTimeDefault, string? runtimeDefault, bool forceNullDefault)>();

            foreach (var p in props)
            {
                string? compileTimeDefault = null;
                string? runtimeDefault = null;
                bool forceNullDefault = false;

                var syntaxRef = p.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef != null)
                {
                    var syntaxNode = syntaxRef.GetSyntax() as PropertyDeclarationSyntax;
                    if (syntaxNode?.Initializer != null)
                    {
                        var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
                        var initCode = syntaxNode.Initializer.Value.ToString();

                        //bool isCompileTimeConst =
                        //    syntaxNode.Initializer.Value is LiteralExpressionSyntax ||
                        //    initCode == "null" ||
                        //    initCode.StartsWith("\"") ||
                        //    initCode.All(c => char.IsDigit(c) || "+-.fFdDlLmMuU".Contains(c)) ||
                        //    p.Type.IsValueType;

                        var isCompileTimeConst = semanticModel.GetConstantValue(syntaxNode.Initializer.Value);

                        if (isCompileTimeConst.HasValue)
                        {
                            compileTimeDefault = initCode;
                        }
                        else
                        {
                            runtimeDefault = initCode;
                            forceNullDefault = true;
                        }
                    }
                }

                // 如果无初始化器且可空 → 默认 null
                if (compileTimeDefault == null && runtimeDefault == null &&
                    p.Type.NullableAnnotation == NullableAnnotation.Annotated)
                {
                    compileTimeDefault = "null";
                }

                parametersWithDefaults.Add((p, compileTimeDefault, runtimeDefault, forceNullDefault));
            }

            // 参数分组
            var requiredParams = parametersWithDefaults.Where(x => x.compileTimeDefault == null && !x.forceNullDefault).ToList();
            var optionalParams = parametersWithDefaults.Where(x => x.compileTimeDefault != null || x.forceNullDefault).ToList();

            bool first = true;
            foreach (var item in requiredParams.Concat(optionalParams))
            {
                if (!first) sb.Append(", ");
                first = false;

                var typeName = (item.runtimeDefault != null || item.forceNullDefault) &&
                               item.prop.Type.NullableAnnotation != NullableAnnotation.Annotated
                    ? item.prop.Type.WithNullableAnnotation(NullableAnnotation.Annotated).ToDisplayString()
                    : item.prop.Type.ToDisplayString();

                sb.Append(typeName).Append(' ').Append(item.prop.Name);

                if (item.compileTimeDefault != null)
                {
                    sb.Append(" = ").Append(item.compileTimeDefault);
                }
                else if (item.forceNullDefault)
                {
                    sb.Append(" = null");
                }
            }

            sb.AppendLine(")");
            sb.AppendLine("    {");

            // 运行时默认值处理
            foreach (var item in parametersWithDefaults.Where(x => x.runtimeDefault != null))
            {
                sb.AppendLine($"        if ({item.prop.Name} == null) {item.prop.Name} = {item.runtimeDefault};");
            }

            var propsConcreteTypeName = propsType.TypeKind == TypeKind.Class && !propsType.IsAbstract
                ? propsType.ToDisplayString()
                : "PropsImpl__" + method.Name;
            var needsLocalImpl = propsConcreteTypeName.StartsWith("PropsImpl__", StringComparison.Ordinal);

            sb.AppendLine($"        var __tmp = new {propsConcreteTypeName}()");
            sb.AppendLine("        {");
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                var comma = i == props.Count - 1 ? "" : ",";
                sb.Append("            ").Append(p.Name).Append(" = ").Append(p.Name);
                sb.AppendLine(comma);
            }
            sb.AppendLine("        };");

            var containerFqn = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (method.ReturnsVoid)
            {
                sb.Append("        ").Append(containerFqn).Append('.').Append(method.Name).AppendLine("(__tmp);");
            }
            else
            {
                sb.Append("        return ").Append(containerFqn).Append('.').Append(method.Name).AppendLine("(__tmp);");
            }

            sb.AppendLine("    }");

            if (needsLocalImpl)
            {
                sb.AppendLine();
                sb.AppendLine($"    file sealed class {propsConcreteTypeName}");
                sb.AppendLine("        : " + propsType.ToDisplayString());
                sb.AppendLine("    {");
                foreach (var p in props)
                {
                    var canInit = p.SetMethod is null || p.SetMethod.DeclaredAccessibility == Accessibility.Private
                        ? "init"
                        : "set";
                    sb.Append("        public ").Append(p.Type.ToDisplayString()).Append(' ').Append(p.Name)
                      .Append(" { get; ").Append(canInit).AppendLine("; }");
                }
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");
            if (ns is not null) sb.AppendLine("}");

            var hintName = $"{container.Name}.{method.Name}.ElementOverload.g.cs";
            spc.AddSource(hintName, sb.ToString());
        }

        private static string? GetDefaultPropertyName(AttributeData? attr)
        {
            if (attr is null) return null;
            // 支持两种写法：nameof(props.X) 或 nameof(X) 或直接字面量 "X"
            var arg = attr.NamedArguments.FirstOrDefault(kv => kv.Key == "DefaultProperty").Value;
            if (arg.Value is null) return null;

            var s = arg.Value as string;
            if (!string.IsNullOrWhiteSpace(s)) return s;

            // 若是编译期常量表达式（不太容易还原），退化为 null
            return null;
        }

        private static string NormalizeElementAttribute(AttributeData attr, string? defaultPropName)
        {
            // 还原为源码形式：若提供了 DefaultProperty，则写成 nameof(XXX)
            if (defaultPropName is null) return "[Element]";

            return $"[Element(DefaultProperty = nameof({defaultPropName}))]";
        }

        private static IEnumerable<IPropertySymbol> EnumerateProps(ITypeSymbol propsType)
        {
            // 遍历继承链，收集 public 实例属性（有 get；有 set 或 init；非索引器）
            var cur = propsType;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            while (cur is not null)
            {
                foreach (var m in cur.GetMembers().OfType<IPropertySymbol>())
                {
                    if (m.IsStatic || m.IsIndexer) continue;
                    if (m.DeclaredAccessibility != Accessibility.Public) continue;
                    if (m.GetMethod is null) continue;

                    // 允许 init 或 set
                    if (m.SetMethod is null && !m.IsReadOnly) continue;

                    if (seen.Add(m.Name))
                        yield return m;
                }
                cur = cur.BaseType;
            }
        }
    }
}
