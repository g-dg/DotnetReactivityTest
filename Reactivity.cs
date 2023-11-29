namespace GarnetDG.Reactivity;

using System.Reflection;
using System.Runtime.CompilerServices;

// Based on https://vuejs.org/guide/extras/reactivity-in-depth.html

public interface IReactive { }

public class ReactivityContext
{
    /// <summary>
    /// Tracks the subscriptions for each property of each object
    /// </summary>
    private readonly ConditionalWeakTable<object, Dictionary<string, HashSet<Action>>> subscriptions = [];

#if DEBUG
    private readonly ConditionalWeakTable<object, string> _objectIds = [];
    private string _getObjectId(object obj)
    {
        var objectId = _objectIds.TryGetValue(obj, out var _objectId) ? _objectId : null;
        if (objectId == null)
        {
            objectId = Guid.NewGuid().ToString();
            _objectIds.Add(obj, objectId);
        }
        return objectId;
    }
#endif

    private Action? activeEffect = null;

    public ReactivityContext() { }

    public void Track(object target, string key)
    {
#if DEBUG
        Console.WriteLine($"Track \"{_getObjectId(target)}\" \"{key}\"");
#endif

        if (activeEffect != null)
        {
            var effects = GetSubscribersForProperty(target, key);
            effects.Add(activeEffect);
        }
    }

    public void Trigger(object target, string key)
    {
#if DEBUG
        Console.WriteLine($"Trigger \"{_getObjectId(target)}\" \"{key}\"");
#endif

        var effects = GetSubscribersForProperty(target, key);
        foreach (var effect in effects)
        {
            effect();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <returns></returns>
    public T Reactive<T>(T obj) where T : class, IReactive
    {
        return ReactiveProxy<T>.Create(obj, this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="effect"></param>
    public void WatchEffect(Action effect)
    {
        activeEffect = effect;
        effect();
        activeEffect = null;
    }

    public IRef<T> Ref<T>(T target)
    {
        return Reactive<IRef<T>>(new Ref<T> { Value = target });
    }

    private HashSet<Action> GetSubscribersForProperty(object target, string key)
    {
        var objectKeys = subscriptions.TryGetValue(target, out var _objectKeys) ? _objectKeys : null;
        if (objectKeys == null)
        {
            objectKeys = [];
            subscriptions.Add(target, objectKeys);
        }

        var keyEffects = objectKeys.TryGetValue(key, out var _keyEffects) ? _keyEffects : null;
        if (keyEffects == null)
        {
            keyEffects = [];
            objectKeys.Add(key, keyEffects);
        }

        return keyEffects;
    }
}

public class Ref<T> : IRef<T>
{
    public T Value { get; set; } = default!;
}
public interface IRef<T> : IReactive
{
    public T Value { get; set; }
}

/// <summary>
/// Proxy that intercepts gets and sets
/// </summary>
/// <typeparam name="T"></typeparam>
internal class ReactiveProxy<T> : DispatchProxy where T : class
{
    private T target = default!;
    private ReactivityContext context = default!;

    public ReactiveProxy() : base()
    {
    }

    /// <summary>
    /// Creates a new ReactiveProxy
    /// </summary>
    /// <param name="target"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public static T Create(T target, ReactivityContext context)
    {
        var proxy = Create<T, ReactiveProxy<T>>() as ReactiveProxy<T>;

        if (proxy is not null)
        {
            proxy.target = target ?? Activator.CreateInstance<T>();
            proxy.context = context;

            // recurse into properties
            var properties = target?.GetType().GetProperties() ?? [];
            foreach (var property in properties)
            {
                // only make reactive if it implements IReactive
                if (typeof(IReactive).IsAssignableFrom(property.PropertyType) && property.PropertyType.IsInterface)
                {
                    if (property.CanRead && property.CanWrite)
                    {
                        var originalValue = property.GetValue(target);
                        var createMethod = typeof(ReactivityContext).GetMethod("Reactive");
                        var reactiveValue = createMethod!.MakeGenericMethod([property.PropertyType]).Invoke(context, [originalValue]);
                        property.SetValue(target, reactiveValue);
                    }
                }
            }

            return (proxy as T)!;
        }
        else
        {
            return default!;
        }
    }

    /// <summary>
    /// Called when a method on the reactive proxy is invoked
    /// </summary>
    /// <param name="targetMethod"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        var isGetter = false;
        var isSetter = false;
        string? propertyName = null;
        if (targetMethod.IsSpecialName)
        {
            if (targetMethod.Name.StartsWith("get_"))
            {
                isGetter = true;
                propertyName = targetMethod.Name[4..];
            }
            if (targetMethod.Name.StartsWith("set_"))
            {
                isSetter = true;
                propertyName = targetMethod.Name[4..];
            }
        }

        if (isGetter && propertyName is not null)
        {
            // if the value is read, track a dependency
            context.Track(target, propertyName);
        }

        // run method
        var result = targetMethod.Invoke(target, args);

        if (isSetter && propertyName is not null)
        {
            // if the value is set, cause a trigger
            context.Trigger(target, propertyName);
        }

        return result;
    }
}
