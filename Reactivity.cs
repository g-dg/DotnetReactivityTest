namespace GarnetDG.Reactivity;

using System.Reflection;
using System.Runtime.CompilerServices;

public interface IReactive { }

public class ReactivityContext
{
    private readonly ConditionalWeakTable<object, Dictionary<string, HashSet<Action>>> subscriptions = [];

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

    private Action? activeEffect = null;

    public ReactivityContext() { }

    public void Track(object target, string key)
    {
        Console.WriteLine($"Track \"{_getObjectId(target)}\" \"{key}\"");

        if (activeEffect != null)
        {
            var effects = getSubscribersForProperty(target, key);
            effects.Add(activeEffect);
        }
    }

    public void Trigger(object target, string key)
    {
        Console.WriteLine($"Trigger \"{_getObjectId(target)}\" \"{key}\"");

        var effects = getSubscribersForProperty(target, key);
        foreach (var effect in effects)
        {
            effect();
        }
    }

    public void WatchEffect(Action effect)
    {
        activeEffect = effect;
        effect();
        activeEffect = null;
    }

    private HashSet<Action> getSubscribersForProperty(object target, string key)
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

    public T Reactive<T>(T obj) where T : class, IReactive
    {
        return ReactiveProxy<T>.Create(obj, this);
    }
}

public class ReactiveProxy<T> : DispatchProxy where T : class
{
    private T target = default!;
    private ReactivityContext context = default!;

    public ReactiveProxy() : base()
    {
    }

    public static T Create(T target, ReactivityContext context)
    {
        var proxy = Create<T, ReactiveProxy<T>>() as ReactiveProxy<T>;

        if (proxy is not null)
        {
            proxy.target = target ?? Activator.CreateInstance<T>();
            proxy.context = context;

            return (proxy as T)!;
        }
        else
        {
            return default!;
        }
    }

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
            context.Track(target, propertyName);
        }

        var result = targetMethod.Invoke(target, args);

        if (isSetter && propertyName is not null)
        {
            context.Trigger(target, propertyName);
        }

        return result;
    }
}
