﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Shiny.Stores.Impl;


public class ObjectStoreBinder : IObjectStoreBinder, IDisposable
{
    readonly Dictionary<object, IKeyValueStore> bindings = new();
    readonly List<INotifyPropertyChanged> boundObjects = new();
    readonly ILogger logger;
    readonly IKeyValueStoreFactory factory;


    public ObjectStoreBinder(IKeyValueStoreFactory factory, ILogger<ObjectStoreBinder> logger)
    {
        this.factory = factory;
        this.logger = logger;
    }


    public void Bind(INotifyPropertyChanged npc, string? keyValueStoreAlias = null)
    {
        IKeyValueStore? store = null;

        if (keyValueStoreAlias != null)
        {
            store = this.factory.GetStore(keyValueStoreAlias);
        }
        else
        {
            keyValueStoreAlias = npc.GetType().GetCustomAttribute<ObjectStoreBinderAttribute>()?.StoreAlias;
            if (keyValueStoreAlias != null)
                store = this.factory.GetStore(keyValueStoreAlias); // error if attribute is bad
        }

        this.Bind(npc, store ?? this.factory.DefaultStore);
    }


    public void Bind(INotifyPropertyChanged npc, IKeyValueStore store)
    {
        try
        {
            var type = npc.GetType();
            var props = this.GetTypeProperties(type).ToList();

            // Skip if there are no properties to bind
            if (props.Count == 0)
            {
                this.logger?.LogInformation($"Skip model bind (no public props): {npc.GetType().FullName} to store: {store.Alias}");
                return;
            }

            foreach (var prop in props)
            {
                var key = GetBindingKey(type, prop);
                if (store.Contains(key))
                {
                    var value = store.Get(prop.PropertyType, key);
                    try
                    {
                        prop.SetValue(npc, value);
                    }
                    catch (Exception ex)
                    {
                        this.logger?.LogError($"Failed to bind {prop.Name} on {type.FullName}", ex);
                    }
                }
            }
            npc.PropertyChanged += this.OnPropertyChanged;
            this.boundObjects.Add(npc);

            this.logger?.LogInformation($"Successfully bound model: {npc.GetType().FullName} to store: {store.Alias}");
            this.bindings.Add(npc, store);
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, $"Failed to bind model: {npc?.GetType().FullName ?? "Unknown"} to store: {store.Alias}");
        }
    }


    public virtual void UnBind(INotifyPropertyChanged obj)
    {
        obj.PropertyChanged -= this.OnPropertyChanged;
        this.boundObjects.Remove(obj);
    }


    public virtual void UnBindAll()
    {
        foreach (var boundObj in this.boundObjects)
            boundObj.PropertyChanged -= this.OnPropertyChanged;

        this.boundObjects.Clear();
    }


    public static string GetBindingKey(Type type, PropertyInfo prop)
        => GetBindingKey(type, prop.Name);


    public static string GetBindingKey(Type type, string propertyName)
        => $"{type.Namespace}.{type.Name}.{propertyName}";

    /// <summary>
    /// Get all type properties with public get and set accessors
    /// </summary>
    protected virtual IEnumerable<PropertyInfo> GetTypeProperties(Type type) => type
        .GetTypeInfo()
        .DeclaredProperties
        .Where(x =>
            (x.GetGetMethod() != null) &&
            (x.GetSetMethod() != null)
        );


    protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
        var prop = this
            .GetTypeProperties(sender.GetType())
            .FirstOrDefault(x => x.Name.Equals(args.PropertyName));

        if (prop != null)
        {
            var key = GetBindingKey(sender.GetType(), prop);
            var value = prop.GetValue(sender);

            if (!this.bindings.ContainsKey(sender))
                throw new ArgumentException("No key/value store found for current binding object - " + sender.GetType().FullName);

            this.bindings[sender].SetOrRemove(key, value);
        }
    }

    public void Dispose() => this.UnBindAll();
}
