using System;
using System.Collections.Immutable;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Types;

namespace Cedar.Ast;

public static class CedarAst
{
    private static readonly Position DefaultPosition = new(string.Empty, 0, 0, 0);

    public static PolicyBuilder Permit()
    {
        return new PolicyBuilder(new PolicyAst(
            Effect.Permit,
            new ScopeAll(),
            new ScopeAll(),
            new ScopeAll(),
            ImmutableArray<INode>.Empty,
            ImmutableArray<Annotation>.Empty,
            DefaultPosition));
    }

    public static PolicyBuilder Forbid()
    {
        return new PolicyBuilder(new PolicyAst(
            Effect.Forbid,
            new ScopeAll(),
            new ScopeAll(),
            new ScopeAll(),
            ImmutableArray<INode>.Empty,
            ImmutableArray<Annotation>.Empty,
            DefaultPosition));
    }

    public static AnnotationBuilder Annotation(string key, string value)
    {
        return Annotation(new Ident(key), new CedarString(value));
    }

    public static AnnotationBuilder Annotation(Ident key, CedarString value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new AnnotationBuilder(ImmutableArray.Create(new Annotation(key, value)));
    }
}

public sealed record Annotation(Ident Key, CedarString Value);

public sealed class AnnotationBuilder
{
    private readonly ImmutableArray<Annotation> _annotations;

    internal AnnotationBuilder(ImmutableArray<Annotation> annotations)
    {
        _annotations = annotations;
    }

    public AnnotationBuilder Annotation(string key, string value)
    {
        return Annotation(new Ident(key), new CedarString(value));
    }

    public AnnotationBuilder Annotation(Ident key, CedarString value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int index = -1;
        for (int i = 0; i < _annotations.Length; i++)
        {
            if (_annotations[i].Key.Equals(key))
            {
                index = i;
                break;
            }
        }

        Annotation annotation = new(key, value);
        return index < 0
            ? new AnnotationBuilder(_annotations.Add(annotation))
            : new AnnotationBuilder(_annotations.SetItem(index, annotation));
    }

    public PolicyBuilder Permit()
    {
        return CedarAst.Permit().WithAnnotations(_annotations);
    }

    public PolicyBuilder Forbid()
    {
        return CedarAst.Forbid().WithAnnotations(_annotations);
    }
}
