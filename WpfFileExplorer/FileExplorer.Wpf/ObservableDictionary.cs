using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FileExplorer.Wpf
{
  [Serializable]
  public class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INotifyPropertyChanged, INotifyCollectionChanged
  {
    #region constructors

    public ObservableDictionary() : base()
    {
      this.KeyValueMap = new Dictionary<TKey, TValue>();
      this.Keys = new ObservableCollection<TKey>();
      this.Values = new ObservableCollection<TValue>();
      //this.KeyValueMap = new SortedDictionary<TKey, TValue

      this.CollectionChanged += HandlePropertyChangedDelegationOnItemAdded;
    }

    public ObservableDictionary(IDictionary<TKey, TValue> dictionary) : this()
    {
      this.KeyValueMap = new Dictionary<TKey, TValue>(dictionary);
      //this.KeyValueMap = new SortedDictionary<TKey, TValue>(dictionary);
    }

    #endregion

    #region Implementation of IEnumerable

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => this.KeyValueMap.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region Implementation of ICollection<KeyValuePair<TKey,TValue>>

    public void Add(KeyValuePair<TKey, TValue> item)
    {
      if (this.IsReadOnly)
      {
        throw new InvalidOperationException(ObservableDictionary<TKey, TValue>.ModifyingReadOnlyCollectionMessage);
      }

      this.KeyValueMap?.Add(item.Key, item.Value);
      UpdateKeyValueProperties();
      OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
      NotifyDictionaryPropertyChanged();
    }

    public void Clear()
    {
      if (this.IsReadOnly)
      {
        throw new InvalidOperationException(ObservableDictionary<TKey, TValue>.ModifyingReadOnlyCollectionMessage);
      }

      OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, this.KeyValueMap.ToList()));
      this.KeyValueMap?.Clear();
      UpdateKeyValueProperties();
      NotifyDictionaryPropertyChanged();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) => this.KeyValueMap.Contains(item);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => (this.KeyValueMap as ICollection<KeyValuePair<TKey, TValue>>).CopyTo(array, arrayIndex);
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
      if (this.IsReadOnly)
      {
        throw new InvalidOperationException(ObservableDictionary<TKey, TValue>.ModifyingReadOnlyCollectionMessage);
      }

      bool removeItemSuccessful = this.KeyValueMap.Remove(item.Key);
      if (removeItemSuccessful)
      {
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
        NotifyDictionaryPropertyChanged();
      }
      return removeItemSuccessful;
    }

    public int Count => this.KeyValueMap.Count;
    public bool IsReadOnly { get; set; }

    #endregion

    #region Implementation of IDictionary<TKey,TValue>

    public bool ContainsValue(TValue value) => this.KeyValueMap.ContainsValue(value);
    public bool ContainsKey(TKey key) => key != null &&  this.KeyValueMap.ContainsKey(key);
    public void Add([NotNull] TKey key, TValue value)
    {
      if (this.IsReadOnly)
      {
        throw new InvalidOperationException(ObservableDictionary<TKey, TValue>.ModifyingReadOnlyCollectionMessage);
      }
      this.KeyValueMap.Add(key, value);
      UpdateKeyValueProperties();
      OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new KeyValuePair<TKey, TValue>(key, value)));
      NotifyDictionaryPropertyChanged();
    }

    public bool Remove(TKey key)
    {
      if (this.IsReadOnly)
      {
        throw new InvalidOperationException(ObservableDictionary<TKey, TValue>.ModifyingReadOnlyCollectionMessage);
      }

      TValue removedValue = default(TValue);
      if (this.KeyValueMap.ContainsKey(key))
      {
        removedValue = this.KeyValueMap[key];
      }

      bool removeItemSuccessful = this.KeyValueMap.Remove(key);
      if (removeItemSuccessful)
      {
        UpdateKeyValueProperties();
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new KeyValuePair<TKey, TValue>(key, removedValue)));
        NotifyDictionaryPropertyChanged();
      }
      return removeItemSuccessful;
    }

    public bool TryGetValue(TKey key, out TValue value) => this.KeyValueMap.TryGetValue(key, out value);

    public TValue this[TKey key]
    {
      get { return this.KeyValueMap[key]; }
      set
      {
        if (this.IsReadOnly)
        {
          throw new InvalidOperationException(ObservableDictionary<TKey, TValue>.ModifyingReadOnlyCollectionMessage);
        }

        bool replaceRequired = this.KeyValueMap.TryGetValue(key, out TValue existingValue);
        this.KeyValueMap[key] = value;
        UpdateKeyValueProperties();

        if (replaceRequired)
        {
          OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, new KeyValuePair<TKey, TValue>(key, value), new KeyValuePair<TKey, TValue>(key, existingValue)));
        }
        else
        {
          OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new KeyValuePair<TKey, TValue>(key, value)));
        }

        NotifyDictionaryPropertyChanged();
      }
    }

    private void UpdateKeyValueProperties()
    {
      this.Keys = new ObservableCollection<TKey>(this.KeyValueMap.Keys);
      this.Values = new ObservableCollection<TValue>(this.KeyValueMap.Values);
    }

    ICollection<TKey> IDictionary<TKey, TValue>.Keys => this.KeyValueMap.Keys.ToList();
    ICollection<TValue> IDictionary<TKey, TValue>.Values => this.KeyValueMap.Values.ToList();

    public ObservableCollection<TKey> Keys { get; set; }

    public ObservableCollection<TValue> Values { get; set; }

    #endregion

    public event PropertyChangedEventHandler PropertyChanged;
    public event NotifyCollectionChangedEventHandler CollectionChanged;
    protected Dictionary<TKey, TValue> KeyValueMap { get; }
    internal const string ModifyingReadOnlyCollectionMessage = @"Unable to modify collection marked as read-only";
    protected bool CollectionChangeIsInternal { get; set; }

    private void HandlePropertyChangedDelegationOnItemAdded(object sender, NotifyCollectionChangedEventArgs e)
    {
      if (this.CollectionChangeIsInternal)
      {
        this.CollectionChangeIsInternal = false;
        return;
      }

      if (e.Action == NotifyCollectionChangedAction.Remove)
      {
        e.OldItems.Cast<KeyValuePair<TKey, TValue>>().ToList().ForEach(
          (entry) =>
          {
            if (entry.Value is INotifyPropertyChanged propertyChangedImplementor)
            {
              propertyChangedImplementor.PropertyChanged -= DelegatePropertyChangedOnItemPropertyChanges;
            }
          });
      }
      else if (e.Action == NotifyCollectionChangedAction.Add)
      {
        e.NewItems.Cast<KeyValuePair<TKey, TValue>>().ToList().ForEach(
          (entry) =>
          {
            if (entry.Value is INotifyPropertyChanged propertyChangedImplementor)
            {
              propertyChangedImplementor.PropertyChanged += DelegatePropertyChangedOnItemPropertyChanges;
            }
          });
      }
      else if (e.Action == NotifyCollectionChangedAction.Replace)
      {
        e.OldItems.Cast<KeyValuePair<TKey, TValue>>().ToList().ForEach(
          (entry) =>
          {
            if (entry.Value is INotifyPropertyChanged propertyChangedImplementor)
            {
              propertyChangedImplementor.PropertyChanged -= DelegatePropertyChangedOnItemPropertyChanges;
            }
          });

        e.NewItems.Cast<KeyValuePair<TKey, TValue>>().ToList().ForEach(
          (entry) =>
          {
            if (entry.Value is INotifyPropertyChanged propertyChangedImplementor)
            {
              propertyChangedImplementor.PropertyChanged += DelegatePropertyChangedOnItemPropertyChanges;
            }
          });
      }
    }

    private void DelegatePropertyChangedOnItemPropertyChanges(object sender, PropertyChangedEventArgs e)
    {
      this.CollectionChangeIsInternal = true;
      OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, sender));
      NotifyDictionaryPropertyChanged();
    }

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void NotifyDictionaryPropertyChanged()
    {
      OnPropertyChanged(nameof(this.Count));
      OnPropertyChanged(nameof(this.Keys));
      OnPropertyChanged(nameof(this.Values));
    }

    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
      this.CollectionChanged?.Invoke(this, e);
    }
  }
}
