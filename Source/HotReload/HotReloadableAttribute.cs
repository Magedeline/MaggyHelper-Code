namespace MaggyHelper.HotReload;

/// <summary>
/// Marks a class or method as supporting hot code reloading.
/// 
/// When applied to a class, all methods in that class can be hot-swapped.
/// When applied to a method, only that specific method can be hot-swapped.
/// 
/// Usage:
/// <code>
/// [HotReloadable]
/// public class MyEntity : Entity
/// {
///     public override void Update() { /* Can be hot-reloaded */ }
/// }
/// 
/// // Or for specific methods:
/// public class MyOtherEntity : Entity
/// {
///     [HotReloadable]
///     public void MyMethod() { /* Can be hot-reloaded */ }
///     
///     public void OtherMethod() { /* Cannot be hot-reloaded */ }
/// }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Struct, 
                AllowMultiple = false, 
                Inherited = true)]
public class HotReloadableAttribute : Attribute
{
    /// <summary>
    /// Optional description for documentation purposes.
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// If true, state preservation will be attempted during reload.
    /// Default is false.
    /// </summary>
    public bool PreserveState { get; set; } = false;
    
    /// <summary>
    /// If true, a callback will be invoked after hot reload.
    /// The class must implement IHotReloadCallback.
    /// </summary>
    public bool NotifyOnReload { get; set; } = false;
    
    /// <summary>
    /// Priority for reload order. Higher values reload first.
    /// Default is 0.
    /// </summary>
    public int Priority { get; set; } = 0;
    
    /// <summary>
    /// Create a new HotReloadable attribute.
    /// </summary>
    public HotReloadableAttribute() { }
    
    /// <summary>
    /// Create a new HotReloadable attribute with description.
    /// </summary>
    /// <param name="description">Description of what this reloadable element does.</param>
    public HotReloadableAttribute(string description)
    {
        Description = description;
    }
}

/// <summary>
/// Marks a class as NOT supporting hot code reloading.
/// Use this to exclude specific classes or methods from hot reload
/// when a parent class is marked as HotReloadable.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Struct, 
                AllowMultiple = false, 
                Inherited = false)]
public class NoHotReloadAttribute : Attribute
{
    /// <summary>
    /// Reason why this element should not be hot-reloaded.
    /// </summary>
    public string Reason { get; set; }
    
    public NoHotReloadAttribute() { }
    
    public NoHotReloadAttribute(string reason)
    {
        Reason = reason;
    }
}

/// <summary>
/// Interface for classes that want to be notified when hot reload occurs.
/// Implement this interface and set NotifyOnReload = true on the HotReloadable attribute.
/// </summary>
public interface IHotReloadCallback
{
    /// <summary>
    /// Called after the class has been hot-reloaded.
    /// Use this to reinitialize any state that may have been lost.
    /// </summary>
    void OnHotReloaded();
    
    /// <summary>
    /// Called before hot reload begins.
    /// Use this to save any important state.
    /// </summary>
    void OnBeforeHotReload();
}

/// <summary>
/// Marks a field to be preserved during hot reload.
/// The field's value will be copied from the old instance to the new one.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public class PreserveOnReloadAttribute : Attribute
{
    /// <summary>
    /// If true, perform a deep copy instead of reference copy.
    /// Default is false (reference copy for reference types).
    /// </summary>
    public bool DeepCopy { get; set; } = false;
}

/// <summary>
/// Marks a method to be called after hot reload is complete.
/// This is an alternative to implementing IHotReloadCallback for simple cases.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class OnHotReloadedAttribute : Attribute
{
    /// <summary>
    /// Order in which the callback is invoked (lower = earlier).
    /// Default is 0.
    /// </summary>
    public int Order { get; set; } = 0;
}

/// <summary>
/// Marks code that is specifically for development/hot reload and should be
/// excluded from release builds.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
[System.Diagnostics.Conditional("DEBUG")]
[System.Diagnostics.Conditional("HOTRELOAD")]
public class DevOnlyAttribute : Attribute
{
    public string Note { get; set; }
    
    public DevOnlyAttribute() { }
    public DevOnlyAttribute(string note) { Note = note; }
}
