using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Cedar.Ast;
using Cedar.Types;

namespace Cedar.Core;

public sealed class Annotations : IReadOnlyDictionary<Ident, CedarString>
{
    private readonly ImmutableArray<Annotation> _annotations;

    internal Annotations(ImmutableArray<Annotation> annotations)
    {
        _annotations = annotations;
    }

    public CedarString this[Ident key]
    {
        get
        {
            if (!TryGetValue(key, out CedarString value))
            {
                throw new KeyNotFoundException($"Annotation '{key.Value}' was not found.");
            }

            return value;
        }
    }

    public IEnumerable<Ident> Keys
    {
        get
        {
            foreach (Annotation annotation in _annotations)
            {
                yield return annotation.Key;
            }
        }
    }

    public IEnumerable<CedarString> Values
    {
        get
        {
            foreach (Annotation annotation in _annotations)
            {
                yield return annotation.Value;
            }
        }
    }

    public int Count => _annotations.Length;

    public bool ContainsKey(Ident key)
    {
        for (int i = 0; i < _annotations.Length; i++)
        {
            if (_annotations[i].Key.Equals(key))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetValue(Ident key, out CedarString value)
    {
        for (int i = 0; i < _annotations.Length; i++)
        {
            Annotation annotation = _annotations[i];
            if (annotation.Key.Equals(key))
            {
                value = annotation.Value;
                return true;
            }
        }

        value = new CedarString(string.Empty);
        return false;
    }

    public IEnumerator<KeyValuePair<Ident, CedarString>> GetEnumerator()
    {
        for (int i = 0; i < _annotations.Length; i++)
        {
            Annotation annotation = _annotations[i];
            yield return new KeyValuePair<Ident, CedarString>(annotation.Key, annotation.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
