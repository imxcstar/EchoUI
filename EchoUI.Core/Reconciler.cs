using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EchoUI.Core
{
    public class Reconciler
    {
        private readonly IRenderer _renderer;
        private readonly object _rootContainer;
        private readonly IUpdateScheduler _scheduler;
        private ComponentInstance? _rootInstance;
        private readonly Dictionary<Type, object> _sharedStates = new();
        private readonly HashSet<ComponentInstance> _dirtyComponents = new();
        private bool _isUpdateQueued = false;

        public Reconciler(IRenderer renderer, object rootContainer)
        {
            _renderer = renderer;
            _rootContainer = rootContainer;
            _scheduler = renderer.GetScheduler(rootContainer);
        }

        public T GetSharedState<T>() where T : class, new()
        {
            if (!_sharedStates.ContainsKey(typeof(T)))
            {
                _sharedStates[typeof(T)] = new T();
            }
            return (T)_sharedStates[typeof(T)];
        }

        internal TextMeasurementResult MeasureText(TextMeasurementRequest request)
        {
            return _renderer.MeasureText(request);
        }

        internal Task<string> ReadClipboardTextAsync()
        {
            return _renderer.ReadClipboardTextAsync();
        }

        internal Task WriteClipboardTextAsync(string text)
        {
            return _renderer.WriteClipboardTextAsync(text);
        }

        public async Task Mount(Delegate rootComponentDelegate)
        {
            try
            {
                var methodInfo = rootComponentDelegate.Method;
                Element? rootElement;

                if (methodInfo.ReturnType.IsAssignableTo(typeof(Task)))
                {
                    var asyncComponent = (AsyncComponent)Delegate.CreateDelegate(typeof(AsyncComponent), rootComponentDelegate.Target, methodInfo);
                    rootElement = new Element(asyncComponent, new RootProps());
                }
                else
                {
                    var component = (Component)Delegate.CreateDelegate(typeof(Component), rootComponentDelegate.Target, methodInfo);
                    rootElement = new Element(component, new RootProps());
                }

                _rootInstance = new ComponentInstance(rootElement, null, this);
                if (_renderer is IInstanceBindingRenderer bindingRenderer)
                {
                    bindingRenderer.AttachRootInstance(_rootInstance);
                }

                var rendered = await RenderComponent(_rootInstance, _rootInstance.Element.Props);
                if (rendered != null)
                {
                    var childInstance = new ComponentInstance(rendered, _rootInstance, this);
                    _rootInstance.Children.Add(childInstance);
                    await MountInstance(childInstance);
                }
            }
            catch (Exception ex)
            {
                var diagnosticException = CreateDiagnosticException("mounting root component", _rootInstance, ex);
                ReportDiagnosticException(diagnosticException);
                throw diagnosticException;
            }
        }

        private void ScheduleUpdate(ComponentInstance instance)
        {
            _dirtyComponents.Add(instance);
            if (!_isUpdateQueued)
            {
                _isUpdateQueued = true;
                _scheduler.Schedule(async () =>
                {
                    try
                    {
                        await ProcessUpdates();
                    }
                    catch (Exception ex)
                    {
                        var diagnosticException = CreateDiagnosticException("processing scheduled UI updates", instance, ex);
                        ReportDiagnosticException(diagnosticException);
                        throw diagnosticException;
                    }
                });
            }
        }

        private async Task ProcessUpdates()
        {
            var componentsToProcess = _dirtyComponents.ToHashSet();
            _dirtyComponents.Clear();
            _isUpdateQueued = false;

            foreach (var instance in componentsToProcess)
            {
                await UpdateInstance(instance);
            }

            if (_dirtyComponents.Count > 0 && !_isUpdateQueued)
            {
                ScheduleUpdate(_dirtyComponents.First());
            }
        }

        private async Task<Element?> RenderComponent(ComponentInstance instance, Props props)
        {
            var elementType = instance.Element.Type;

            var oldContext = Hooks.Context;
            Hooks.Context = new HookContext { Instance = instance, ScheduleUpdate = ScheduleUpdate };
            instance.HookIndex = 0;
            Element? resultElement = null;
            try
            {
                if (elementType.IsComponent)
                {
                    resultElement = ((Component)elementType.AsComponentDelegate)(props);
                }
                else if (elementType.IsAsyncComponent)
                {
                    var renderVersion = ++instance.AsyncRenderVersion;
                    var renderTask = ((AsyncComponent)elementType.AsComponentDelegate)(props);
                    instance.RenderingTask = renderTask;
                    if (renderTask.IsCompletedSuccessfully)
                    {
                        resultElement = renderTask.Result;
                        instance.HasCompletedInitialRender = true;
                        instance.IsAsyncPlaceholder = false;
                    }
                    else
                    {
                        if (!instance.HasCompletedInitialRender)
                        {
                            instance.IsAsyncPlaceholder = true;
                            _ = renderTask.ContinueWith(task =>
                            {
                                if (!ReferenceEquals(instance.RenderingTask, task) || renderVersion != instance.AsyncRenderVersion)
                                    return;

                                if (task.IsCanceled)
                                    return;

                                if (task.Exception != null)
                                {
                                    var diagnosticException = CreateDiagnosticException("rendering async component", instance, task.Exception.GetBaseException());
                                    ReportDiagnosticException(diagnosticException);
                                    return;
                                }

                                instance.HasCompletedInitialRender = true;
                                instance.IsAsyncPlaceholder = false;
                                ScheduleUpdate(instance);
                            }, TaskScheduler.FromCurrentSynchronizationContext());
                            resultElement = props.Fallback;
                        }
                        else
                        {
                            resultElement = await renderTask;
                            if (!ReferenceEquals(instance.RenderingTask, renderTask) || renderVersion != instance.AsyncRenderVersion)
                                return instance.Children.FirstOrDefault()?.Element;
                            instance.HasCompletedInitialRender = true;
                            instance.IsAsyncPlaceholder = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw CreateDiagnosticException("rendering component", instance, ex);
            }
            finally
            {
                Hooks.Context = oldContext;
            }
            return resultElement;
        }

        private async Task MountInstance(ComponentInstance instance)
        {
            try
            {
                var element = instance.Element;
                var elementType = element.Type;

            if (elementType.IsNative)
            {
                instance.NativeElement = _renderer.CreateElement(elementType.AsNativeType);
                if (_renderer is IInstanceBindingRenderer bindingRenderer)
                {
                    bindingRenderer.BindNativeElement(instance.NativeElement, instance);
                }
                var initialPatch = CreateInitialPatch(element.Props);
                if (initialPatch != null)
                {
                    _renderer.PatchProperties(instance.NativeElement, element.Props, initialPatch);
                }

                if (_renderer is IElementStateRenderer stateRenderer)
                {
                    var stateKey = GetElementStateKey(instance);
                    if (stateKey != null)
                        stateRenderer.RestoreElementState(instance.NativeElement, stateKey);
                }

                var parentContainer = GetParentContainer(instance);
                var index = instance.Parent?.Children.IndexOf(instance) ?? -1;
                _renderer.AddChild(parentContainer, instance.NativeElement, index);

                foreach (var childElement in element.Props.Children)
                {
                    if (childElement == null) continue; // defensive: skip null children
                    var childInstance = new ComponentInstance(childElement, instance, this);
                    instance.Children.Add(childInstance);
                    await MountInstance(childInstance);
                }
            }
            else
            {
                var rendered = await RenderComponent(instance, element.Props);
                if (rendered != null)
                {
                    var childInstance = new ComponentInstance(rendered, instance, this);
                    instance.Children.Add(childInstance);
                    await MountInstance(childInstance);
                }
            }
            }
            catch (Exception ex)
            {
                throw CreateDiagnosticException("mounting element", instance, ex);
            }
        }

        private async Task UpdateInstance(ComponentInstance instance)
        {
            try
            {
                var element = instance.Element;
                var elementType = element.Type;

            if (elementType.IsNative)
            {
                await DiffChildren(instance, element.Props.Children);
            }
            else
            {
                var rendered = await RenderComponent(instance, element.Props);

                if (rendered == null)
                {
                    foreach (var child in instance.Children.ToList())
                    {
                        UnmountInstance(child);
                    }
                    instance.Children.Clear();
                }
                else
                {
                    if (instance.Children.Count == 0)
                    {
                        var childInstance = new ComponentInstance(rendered, instance, this);
                        instance.Children.Add(childInstance);
                        await MountInstance(childInstance);
                    }
                    else
                    {
                        var existingChild = instance.Children[0];
                        await DiffInstance(existingChild, rendered);
                    }
                }
            }
            }
            catch (Exception ex)
            {
                throw CreateDiagnosticException("updating element", instance, ex);
            }
        }

        private async Task DiffInstance(ComponentInstance instance, Element newElement)
        {
            try
            {
                var oldElement = instance.Element;

            if (!ElementTypesMatch(oldElement.Type, newElement.Type) || !ElementKeysMatch(oldElement.Props.Key, newElement.Props.Key))
            {
                var parent = instance.Parent;
                var index = parent?.Children.IndexOf(instance) ?? -1;

                UnmountInstance(instance);

                var newInstance = new ComponentInstance(newElement, parent, this);
                if (parent != null)
                {
                    if (index >= 0 && index < parent.Children.Count)
                        parent.Children.Insert(index, newInstance);
                    else
                        parent.Children.Add(newInstance);
                }

                await MountInstance(newInstance);
                return;
            }

            if (!newElement.Type.IsNative && !instance.IsAsyncPlaceholder && AreComponentPropsEqual(oldElement.Props, newElement.Props))
            {
                instance.Element = newElement;
                return;
            }

            instance.Element = newElement;

            if (newElement.Type.IsNative)
            {
                var patch = DiffProps(oldElement.Props, newElement.Props);
                if (patch != null && patch.UpdatedProperties?.Count > 0)
                {
                    _renderer.PatchProperties(instance.NativeElement!, newElement.Props, patch);
                }

                await DiffChildren(instance, newElement.Props.Children);
            }
            else
            {
                await UpdateInstance(instance);
            }
            }
            catch (Exception ex)
            {
                throw CreateDiagnosticException("diffing element", instance, ex);
            }
        }

        #region Props Diffing Logic

        private PropertyPatch? CreateInitialPatch(Props props)
        {
            var patch = new PropertyPatch { UpdatedProperties = new Dictionary<string, object?>() };
            var hasContent = false;

            foreach (var prop in PropsMetadata.Get(props))
            {
                var value = prop.Getter(props);
                if (!Equals(value, prop.DefaultValue))
                {
                    patch.UpdatedProperties[prop.Name] = value;
                    hasContent = true;
                }
            }

            // Handle NativeProps.Properties by unpacking its contents.
            if (props is NativeProps nativeProps && nativeProps.Properties != null)
            {
                foreach (var kvp in nativeProps.Properties.Value.Data)
                {
                    // We add all values, even if null, because they are explicitly set.
                    patch.UpdatedProperties[kvp.Key] = kvp.Value;
                    hasContent = true;
                }
            }

            return hasContent ? patch : null;
        }

        private PropertyPatch? DiffProps(Props oldProps, Props newProps)
        {
            var patch = new PropertyPatch();
            var updatedProperties = new Dictionary<string, object?>();
            var hasChanges = false;
            var processed = new HashSet<string>(StringComparer.Ordinal);

            foreach (var prop in PropsMetadata.Get(newProps).Concat(PropsMetadata.Get(oldProps)))
            {
                if (!processed.Add(prop.Name))
                    continue;

                var oldValue = TryGetPropertyValue(oldProps, prop.Name, out var oldKnown) ? oldKnown : prop.DefaultValue;
                var newValue = TryGetPropertyValue(newProps, prop.Name, out var newKnown) ? newKnown : prop.DefaultValue;

                var propertyChanged = prop.IsDelegate
                    ? (oldValue == null) != (newValue == null)
                    : !Equals(oldValue, newValue);

                if (propertyChanged)
                {
                    updatedProperties[prop.Name] = newValue;
                    hasChanges = true;
                }
            }

            // Special handling for NativeProps.Properties.
            if (oldProps is NativeProps || newProps is NativeProps)
            {
                var oldNativeProps = oldProps as NativeProps;
                var newNativeProps = newProps as NativeProps;

                var oldDict = oldNativeProps?.Properties?.Data ?? new Dictionary<string, object?>();
                var newDict = newNativeProps?.Properties?.Data ?? new Dictionary<string, object?>();
                var allKeys = oldDict.Keys.Union(newDict.Keys).Distinct();

                foreach (var key in allKeys)
                {
                    oldDict.TryGetValue(key, out var oldPropValue);
                    newDict.TryGetValue(key, out var newPropValue);

                    bool propertyValueChanged;
                    var propValueType = newPropValue?.GetType() ?? oldPropValue?.GetType();

                    if (propValueType != null && typeof(Delegate).IsAssignableFrom(propValueType))
                    {
                        propertyValueChanged = (oldPropValue == null) != (newPropValue == null);
                    }
                    else
                    {
                        propertyValueChanged = !Equals(oldPropValue, newPropValue);
                    }

                    if (propertyValueChanged)
                    {
                        updatedProperties[key] = newPropValue; // newPropValue will be null if the key was removed
                        hasChanges = true;
                    }
                }
            }

            if (hasChanges)
            {
                patch.UpdatedProperties = updatedProperties;
                return patch;
            }

            return null;
        }

        private static bool TryGetPropertyValue(Props props, string name, out object? value)
        {
            foreach (var prop in PropsMetadata.Get(props))
            {
                if (prop.Name == name)
                {
                    value = prop.Getter(props);
                    return true;
                }
            }

            value = null;
            return false;
        }

        #endregion

        private static bool AreComponentPropsEqual(Props oldProps, Props newProps)
        {
            var comparer = newProps.AreEqual ?? oldProps.AreEqual;
            return comparer != null && comparer(oldProps, newProps);
        }

        private async Task DiffChildren(ComponentInstance parent, IReadOnlyList<Element> newChildElements)
        {
            try
            {
                var oldChildren = parent.Children.ToList();
            var newChildren = new List<ComponentInstance>();
            var newInstancesCreated = new List<ComponentInstance>();

            ValidateUniqueChildKeys(parent, newChildElements);
            var oldKeyedChildren = BuildKeyedChildMap(parent, oldChildren);

            var processedOldChildren = new HashSet<ComponentInstance>();

            for (int i = 0; i < newChildElements.Count; i++)
            {
                var newChildElement = newChildElements[i];
                ComponentInstance? matchingChild = null;

                if (newChildElement.Props.Key != null &&
                    oldKeyedChildren.TryGetValue(newChildElement.Props.Key, out var keyedChild) &&
                    !processedOldChildren.Contains(keyedChild))
                {
                    matchingChild = keyedChild;
                }
                else if (newChildElement.Props.Key == null)
                {
                    // Fallback to index-based matching for non-keyed elements.
                    // This finds the next available old child at the current position that matches.
                    if (i < oldChildren.Count &&
                        oldChildren[i].Element.Props.Key == null &&
                        !processedOldChildren.Contains(oldChildren[i]) &&
                        ElementTypesMatch(oldChildren[i].Element.Type, newChildElement.Type))
                    {
                        matchingChild = oldChildren[i];
                    }
                }

                if (matchingChild != null)
                {
                    processedOldChildren.Add(matchingChild);
                    await DiffInstance(matchingChild, newChildElement);
                    newChildren.Add(matchingChild);
                }
                else
                {
                    var newInstance = new ComponentInstance(newChildElement, parent, this);
                    newInstancesCreated.Add(newInstance);
                    await MountInstance(newInstance); // Mount will add it to the DOM
                    newChildren.Add(newInstance);
                }
            }

            foreach (var oldChild in oldChildren)
            {
                if (!processedOldChildren.Contains(oldChild))
                {
                    UnmountInstance(oldChild);
                }
            }

            // Update the component tree structure first.
            parent.Children = newChildren;

            // If the parent is a native element, reorder the children in the DOM efficiently.
            if (parent.NativeElement != null)
            {
                // 1. Figure out the actual order of DOM nodes before we start moving them.
                var domStateBeforeReorder = oldChildren.Where(c => processedOldChildren.Contains(c)).ToList();
                domStateBeforeReorder.AddRange(newInstancesCreated);

                // 2. Iterate through the desired state and only move when necessary.
                for (int i = 0; i < newChildren.Count; i++)
                {
                    var instanceToPlace = newChildren[i];
                    object? nativeToPlace = GetFirstNativeElement(instanceToPlace);
                    if (nativeToPlace == null) continue;

                    object? currentNativeAtPosition = (i < domStateBeforeReorder.Count)
                        ? GetFirstNativeElement(domStateBeforeReorder[i])
                        : null;

                    // 3. If the node that should be at this position is not there, move it.
                    if (!object.ReferenceEquals(nativeToPlace, currentNativeAtPosition))
                    {
                        _renderer.MoveChild(parent.NativeElement, nativeToPlace, i);

                        // 4. Update our simulation of the DOM to reflect the move.
                        var instanceMovedInSim = domStateBeforeReorder.First(inst => GetFirstNativeElement(inst) == nativeToPlace);
                        domStateBeforeReorder.Remove(instanceMovedInSim);
                        domStateBeforeReorder.Insert(i, instanceMovedInSim);
                    }
                }
            }
            }
            catch (Exception ex)
            {
                throw CreateDiagnosticException("diffing children", parent, ex);
            }
        }

        private static Dictionary<string, ComponentInstance> BuildKeyedChildMap(ComponentInstance parent, IReadOnlyList<ComponentInstance> oldChildren)
        {
            var result = new Dictionary<string, ComponentInstance>(StringComparer.Ordinal);
            foreach (var child in oldChildren)
            {
                var key = child.Element.Props.Key;
                if (key == null)
                    continue;

                if (!result.TryAdd(key, child))
                    throw new InvalidOperationException($"Duplicate existing child key \"{key}\" under {DescribeElement(parent.Element)}.");
            }

            return result;
        }

        private static void ValidateUniqueChildKeys(ComponentInstance parent, IReadOnlyList<Element> newChildElements)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in newChildElements)
            {
                var key = child.Props.Key;
                if (key != null && !keys.Add(key))
                    throw new InvalidOperationException($"Duplicate new child key \"{key}\" under {DescribeElement(parent.Element)}.");
            }
        }

        private object GetParentContainer(ComponentInstance instance)
        {
            var parent = instance.Parent;
            while (parent != null)
            {
                if (parent.NativeElement != null)
                    return parent.NativeElement;
                parent = parent.Parent;
            }
            return _rootContainer;
        }

        private string? GetElementStateKey(ComponentInstance instance)
        {
            if (instance.Element.Props.Key == null)
                return null;

            var parts = new Stack<string>();
            for (var current = instance; current != null; current = current.Parent)
            {
                var name = DescribeElementType(current.Element.Type);
                var key = current.Element.Props.Key;
                parts.Push(key == null ? name : $"{name}:{key}");
            }

            return string.Join("/", parts);
        }

        private object? GetFirstNativeElement(ComponentInstance instance)
        {
            if (instance.NativeElement != null)
                return instance.NativeElement;

            foreach (var child in instance.Children)
            {
                var native = GetFirstNativeElement(child);
                if (native != null)
                    return native;
            }

            return null;
        }

        private static bool ElementKeysMatch(string? oldKey, string? newKey)
        {
            return string.Equals(oldKey, newKey, StringComparison.Ordinal);
        }

        private bool ElementTypesMatch(ElementType type1, ElementType type2)
        {
            if (type1.IsNative && type2.IsNative)
                return type1.AsNativeType == type2.AsNativeType;

            return type1.AsComponentDelegate?.Method == type2.AsComponentDelegate?.Method;
        }

        private void UnmountInstance(ComponentInstance instance)
        {
            foreach (var cleanup in instance.EffectCleanups.Values)
                cleanup?.Invoke();

            foreach (var child in instance.Children.ToList())
            {
                UnmountInstance(child);
            }

            if (instance.NativeElement != null)
            {
                if (_renderer is IElementStateRenderer stateRenderer)
                {
                    var stateKey = GetElementStateKey(instance);
                    if (stateKey != null)
                        stateRenderer.SaveElementState(instance.NativeElement, stateKey);
                }

                var container = GetParentContainer(instance);
                if (_renderer is IInstanceBindingRenderer bindingRenderer)
                {
                    bindingRenderer.UnbindNativeElement(instance.NativeElement);
                }
                _renderer.RemoveChild(container, instance.NativeElement);
            }

            instance.Parent?.Children.Remove(instance);
        }

        public void HotSwapRootComponent(Delegate newRootComponent)
        {
            if (_rootInstance != null)
            {
                var methodInfo = newRootComponent.Method;
                ElementType newType;

                if (methodInfo.ReturnType.IsAssignableTo(typeof(Task)))
                {
                    newType = (AsyncComponent)Delegate.CreateDelegate(typeof(AsyncComponent), newRootComponent.Target, methodInfo);
                }
                else
                {
                    newType = (Component)Delegate.CreateDelegate(typeof(Component), newRootComponent.Target, methodInfo);
                }

                _rootInstance.Element = _rootInstance.Element with { Type = newType };
                ScheduleUpdate(_rootInstance);
            }
        }

        private static EchoUIRenderException CreateDiagnosticException(string operation, ComponentInstance? instance, Exception exception)
        {
            if (exception is EchoUIRenderException echoException)
            {
                return echoException;
            }

            return new EchoUIRenderException(operation, BuildElementStack(instance), exception);
        }

        private static void ReportDiagnosticException(EchoUIRenderException exception)
        {
            const string markerKey = "EchoUI.DiagnosticLogged";
            if (exception.Data.Contains(markerKey))
            {
                return;
            }

            exception.Data[markerKey] = true;
            Console.Error.WriteLine(exception.ToString());
            Debug.WriteLine(exception.ToString());
        }

        private static string BuildElementStack(ComponentInstance? instance)
        {
            if (instance == null)
            {
                return "  <no component instance available>";
            }

            var stack = new Stack<ComponentInstance>();
            for (var current = instance; current != null; current = current.Parent)
            {
                stack.Push(current);
            }

            var sb = new StringBuilder();
            var depth = 0;
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                sb.Append("  ");
                sb.Append(new string(' ', depth * 2));
                sb.Append("at ");
                sb.Append(DescribeElement(current.Element));
                sb.AppendLine();
                depth++;
            }

            return sb.ToString().TrimEnd();
        }

        private static string DescribeElement(Element element)
        {
            var name = DescribeElementType(element.Type);
            var key = element.Props.Key;
            return key == null ? name : $"{name} key=\"{key}\"";
        }

        private static string DescribeElementType(ElementType type)
        {
            if (type.IsNative)
            {
                return type.AsNativeType;
            }

            var method = type.AsComponentDelegate?.Method;
            if (method == null)
            {
                return "<unknown component>";
            }

            var declaringType = method.DeclaringType?.Name;
            return string.IsNullOrEmpty(declaringType) ? method.Name : $"{declaringType}.{method.Name}";
        }

        private record class RootProps : Props;
    }
}