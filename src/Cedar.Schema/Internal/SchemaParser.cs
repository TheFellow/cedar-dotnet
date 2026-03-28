using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema.Internal;

internal static class SchemaParser
{
    private const int MaxDepth = 256;
    private const int MaxErrors = 10;

    private static readonly HashSet<string> ReservedTypeNames = new(StringComparer.Ordinal)
    {
        "Bool",
        "Boolean",
        "Entity",
        "Extension",
        "Long",
        "Record",
        "Set",
        "String"
    };

    public static SchemaDocument Parse(string source, string filename = "")
    {
        ArgumentNullException.ThrowIfNull(source);

        IReadOnlyList<SchemaToken> tokens;
        try
        {
            tokens = SchemaTokenizer.Tokenize(source, filename);
        }
        catch (SchemaParseException ex)
        {
            throw new AggregateException([ex]);
        }

        Parser parser = new(tokens);
        return parser.ParseDocument();
    }

    private sealed class Parser
    {
        private readonly ParserState _state;
        private readonly List<Exception> _errors = [];

        public Parser(IReadOnlyList<SchemaToken> tokens)
        {
            _state = new ParserState(tokens);
        }

        public SchemaDocument ParseDocument()
        {
            NamespaceBuilder globalNamespace = new();
            Dictionary<string, NamespaceDecl> namespaces = new(StringComparer.Ordinal);

            ParseDeclarations(globalNamespace, namespaces, stopAtRightBrace: false);

            if (_errors.Count > 0)
            {
                throw new AggregateException(_errors);
            }

            return new SchemaDocument
            {
                GlobalNamespace = globalNamespace.Build(),
                Namespaces = namespaces
            };
        }

        private void ParseDeclarations(NamespaceBuilder targetNamespace, Dictionary<string, NamespaceDecl> namespaces, bool stopAtRightBrace)
        {
            while (!_state.IsAtEnd && (!stopAtRightBrace || !_state.Check(SchemaTokenType.RightBrace)))
            {
                try
                {
                    IReadOnlyList<SchemaAnnotation> annotations = ParseAnnotations();
                    if (_state.CheckIdentifier("namespace"))
                    {
                        _state.Advance();
                        (string name, NamespaceDecl declaration) = ParseNamespace(annotations);
                        if (!namespaces.TryAdd(name, declaration))
                        {
                            throw _state.Error($"namespace {Quote(name)} is declared twice");
                        }
                    }
                    else
                    {
                        ParseDeclaration(annotations, targetNamespace);
                    }
                }
                catch (SchemaParseException ex)
                {
                    _errors.Add(ex);
                    if (_errors.Count >= MaxErrors)
                    {
                        return;
                    }

                    _state.SynchronizeDeclaration(stopAtRightBrace);
                }
            }
        }

        private (string Name, NamespaceDecl Namespace) ParseNamespace(IReadOnlyList<SchemaAnnotation> annotations)
        {
            string path = ParsePath();
            string[] components = path.Split("::", StringSplitOptions.None);
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] == "__cedar")
                {
                    throw _state.Error($"the name {Quote(path)} contains \"__cedar\", which is reserved");
                }
            }

            _state.Expect(SchemaTokenType.LeftBrace, "expected '{'");

            NamespaceBuilder namespaceBuilder = new()
            {
                Annotations = annotations
            };

            ParseDeclarations(namespaceBuilder, new Dictionary<string, NamespaceDecl>(StringComparer.Ordinal), stopAtRightBrace: true);

            if (_state.IsAtEnd)
            {
                throw _state.Error("expected '}' to close namespace, got EOF");
            }

            _state.Expect(SchemaTokenType.RightBrace, "expected '}'");
            return (path, namespaceBuilder.Build());
        }

        private void ParseDeclaration(IReadOnlyList<SchemaAnnotation> annotations, NamespaceBuilder target)
        {
            if (!_state.Check(SchemaTokenType.Identifier))
            {
                throw _state.Error($"expected declaration (entity, action, or type), got {DescribeToken(_state.Current)}");
            }

            string keyword = _state.Advance().Text;
            switch (keyword)
            {
                case "entity":
                    ParseEntityDeclaration(annotations, target);
                    return;
                case "action":
                    ParseActionDeclaration(annotations, target);
                    return;
                case "type":
                    ParseTypeDeclaration(annotations, target);
                    return;
                default:
                    throw _state.Error($"expected declaration (entity, action, or type), got identifier {Quote(keyword)}");
            }
        }

        private void ParseEntityDeclaration(IReadOnlyList<SchemaAnnotation> annotations, NamespaceBuilder target)
        {
            List<Ident> names = ParseIdentifiers();

            if (_state.CheckIdentifier("enum"))
            {
                _state.Advance();
                ParseEnumDeclaration(annotations, names, target);
                return;
            }

            List<EntityType> parents = [];
            if (_state.CheckReservedKeyword("in"))
            {
                _state.Advance();
                parents = ParseEntityTypeList();
            }

            RecordType? shape = null;
            if (_state.Match(SchemaTokenType.Equals))
            {
                shape = ParseRecordType();
            }
            else if (_state.Check(SchemaTokenType.LeftBrace))
            {
                shape = ParseRecordType();
            }

            SchemaType? tags = null;
            if (_state.CheckIdentifier("tags"))
            {
                _state.Advance();
                tags = ParseType();
            }

            _state.Expect(SchemaTokenType.Semicolon, "expected ';'");

            for (int index = 0; index < names.Count; index++)
            {
                Ident name = names[index];
                if (target.Entities.ContainsKey(name) || target.Enums.ContainsKey(name))
                {
                    throw _state.Error($"entity {Quote(name.Value)} is declared twice");
                }

                target.Entities.Add(name, new EntityDecl
                {
                    Annotations = annotations,
                    ParentTypes = parents,
                    Shape = shape,
                    Tags = tags
                });
            }
        }

        private void ParseEnumDeclaration(IReadOnlyList<SchemaAnnotation> annotations, IReadOnlyList<Ident> names, NamespaceBuilder target)
        {
            _state.Expect(SchemaTokenType.LeftBracket, "expected '['");

            List<string> values = [];
            while (!_state.Check(SchemaTokenType.RightBracket))
            {
                SchemaToken token = _state.Expect(SchemaTokenType.String, "expected string literal in enum");
                values.Add(token.Text);

                if (_state.Match(SchemaTokenType.Comma))
                {
                    continue;
                }

                if (!_state.Check(SchemaTokenType.RightBracket))
                {
                    throw _state.Error($"expected ',' or ']', got {DescribeToken(_state.Current)}");
                }
            }

            _state.Expect(SchemaTokenType.RightBracket, "expected ']'");
            _state.Expect(SchemaTokenType.Semicolon, "expected ';'");

            for (int index = 0; index < names.Count; index++)
            {
                Ident name = names[index];
                if (target.Entities.ContainsKey(name) || target.Enums.ContainsKey(name))
                {
                    throw _state.Error($"entity {Quote(name.Value)} is declared twice");
                }

                target.Enums.Add(name, new EnumDecl
                {
                    Annotations = annotations,
                    Values = values
                });
            }
        }

        private void ParseActionDeclaration(IReadOnlyList<SchemaAnnotation> annotations, NamespaceBuilder target)
        {
            List<string> names = ParseNames();

            List<ParentRef> parents = [];
            if (_state.CheckReservedKeyword("in"))
            {
                _state.Advance();
                parents = ParseParentRefs();
            }

            AppliesToDecl? appliesTo = null;
            if (_state.CheckIdentifier("appliesTo"))
            {
                _state.Advance();
                appliesTo = ParseAppliesTo();
            }

            if (_state.CheckIdentifier("attributes"))
            {
                _state.Advance();
                _state.Expect(SchemaTokenType.LeftBrace, "expected '{'");
                _state.Expect(SchemaTokenType.RightBrace, "expected '}'");
            }

            _state.Expect(SchemaTokenType.Semicolon, "expected ';'");

            for (int index = 0; index < names.Count; index++)
            {
                string name = names[index];
                if (target.Actions.ContainsKey(name))
                {
                    throw _state.Error($"action {Quote(name)} is declared twice");
                }

                target.Actions.Add(name, new ActionDecl
                {
                    Annotations = annotations,
                    Parents = parents,
                    AppliesTo = appliesTo
                });
            }
        }

        private void ParseTypeDeclaration(IReadOnlyList<SchemaAnnotation> annotations, NamespaceBuilder target)
        {
            SchemaToken nameToken = _state.Expect(SchemaTokenType.Identifier, "expected type name");
            if (ReservedTypeNames.Contains(nameToken.Text))
            {
                throw _state.Error($"{Quote(nameToken.Text)} is a reserved type name");
            }

            _state.Expect(SchemaTokenType.Equals, "expected '='");
            SchemaType type = ParseType();
            _state.Expect(SchemaTokenType.Semicolon, "expected ';'");

            Ident name = new(nameToken.Text);
            if (target.CommonTypes.ContainsKey(name))
            {
                throw _state.Error($"type {Quote(name.Value)} is declared twice");
            }

            target.CommonTypes.Add(name, new CommonTypeDecl
            {
                Annotations = annotations,
                Type = type
            });
        }

        private IReadOnlyList<SchemaAnnotation> ParseAnnotations()
        {
            List<SchemaAnnotation> annotations = [];
            HashSet<Ident> keys = [];

            while (_state.Match(SchemaTokenType.At))
            {
                SchemaToken token = _state.Current;
                if (!_state.Check(SchemaTokenType.Identifier) && !_state.Check(SchemaTokenType.ReservedKeyword))
                {
                    throw _state.Error($"expected annotation name, got {DescribeToken(_state.Current)}");
                }

                Ident key = new(_state.Advance().Text);
                string value = string.Empty;

                if (_state.Match(SchemaTokenType.LeftParen))
                {
                    value = _state.Expect(SchemaTokenType.String, "expected annotation value string").Text;
                    _state.Expect(SchemaTokenType.RightParen, "expected ')'");
                }

                if (!keys.Add(key))
                {
                    throw new SchemaParseException(token.Position, $"duplicate annotation {Quote(key.Value)}");
                }

                annotations.Add(new SchemaAnnotation(key, value));
            }

            return annotations;
        }

        private string ParsePath()
        {
            SchemaToken first = _state.Current;
            if (!_state.Check(SchemaTokenType.Identifier) && !_state.CheckReservedKeyword("__cedar"))
            {
                throw _state.Error($"expected identifier, got {DescribeToken(first)}");
            }

            string path = _state.Advance().Text;
            while (_state.Match(SchemaTokenType.DoubleColon))
            {
                SchemaToken segment = _state.Expect(SchemaTokenType.Identifier, "expected identifier after '::'");
                path += "::" + segment.Text;
            }

            return path;
        }

        private (string Path, string? Name) ParsePathForReference()
        {
            SchemaToken first = _state.Current;
            if (!_state.Check(SchemaTokenType.Identifier) && !_state.CheckReservedKeyword("__cedar"))
            {
                throw _state.Error($"expected identifier, got {DescribeToken(first)}");
            }

            string path = _state.Advance().Text;
            while (_state.Match(SchemaTokenType.DoubleColon))
            {
                if (_state.Check(SchemaTokenType.String))
                {
                    string name = _state.Advance().Text;
                    return (path, name);
                }

                SchemaToken segment = _state.Expect(SchemaTokenType.Identifier, "expected identifier or string after '::'");
                path += "::" + segment.Text;
            }

            return (path, null);
        }

        private List<Ident> ParseIdentifiers()
        {
            List<Ident> result = [new(_state.Expect(SchemaTokenType.Identifier, "expected identifier").Text)];
            while (_state.Match(SchemaTokenType.Comma))
            {
                result.Add(new Ident(_state.Expect(SchemaTokenType.Identifier, "expected identifier after ','").Text));
            }

            return result;
        }

        private List<string> ParseNames()
        {
            List<string> names = [ParseName()];
            while (_state.Match(SchemaTokenType.Comma))
            {
                names.Add(ParseName());
            }

            return names;
        }

        private string ParseName()
        {
            if (_state.Check(SchemaTokenType.Identifier) || _state.Check(SchemaTokenType.String) || _state.CheckReservedKeyword("__cedar"))
            {
                return _state.Advance().Text;
            }

            throw _state.Error($"expected name (identifier or string), got {DescribeToken(_state.Current)}");
        }

        private List<EntityType> ParseEntityTypeList()
        {
            if (_state.Match(SchemaTokenType.LeftBracket))
            {
                List<EntityType> result = [];
                while (!_state.Check(SchemaTokenType.RightBracket))
                {
                    result.Add(new EntityType(ParsePath()));
                    if (_state.Match(SchemaTokenType.Comma))
                    {
                        continue;
                    }

                    if (!_state.Check(SchemaTokenType.RightBracket))
                    {
                        throw _state.Error($"expected ',' or ']', got {DescribeToken(_state.Current)}");
                    }
                }

                _state.Expect(SchemaTokenType.RightBracket, "expected ']'");
                return result;
            }

            return [new EntityType(ParsePath())];
        }

        private List<ParentRef> ParseParentRefs()
        {
            if (_state.Match(SchemaTokenType.LeftBracket))
            {
                List<ParentRef> result = [];
                while (!_state.Check(SchemaTokenType.RightBracket))
                {
                    result.Add(ParseParentRef());
                    if (_state.Match(SchemaTokenType.Comma))
                    {
                        continue;
                    }

                    if (!_state.Check(SchemaTokenType.RightBracket))
                    {
                        throw _state.Error($"expected ',' or ']', got {DescribeToken(_state.Current)}");
                    }
                }

                _state.Expect(SchemaTokenType.RightBracket, "expected ']'");
                return result;
            }

            return [ParseParentRef()];
        }

        private ParentRef ParseParentRef()
        {
            if (_state.Check(SchemaTokenType.String))
            {
                return new ParentRef(null, _state.Advance().Text);
            }

            (string path, string? qualifiedName) = ParsePathForReference();
            return qualifiedName is null
                ? new ParentRef(null, path)
                : new ParentRef(new EntityType(path), qualifiedName);
        }

        private AppliesToDecl ParseAppliesTo()
        {
            _state.Expect(SchemaTokenType.LeftBrace, "expected '{'");

            List<EntityType>? principals = null;
            List<EntityType>? resources = null;
            RecordType? contextRecord = null;
            TypeRef? contextPath = null;

            while (!_state.Check(SchemaTokenType.RightBrace))
            {
                if (_state.IsAtEnd)
                {
                    throw _state.Error("expected '}' to close appliesTo, got EOF");
                }

                SchemaToken name = _state.Expect(SchemaTokenType.Identifier, "expected 'principal', 'resource', or 'context'");
                _state.Expect(SchemaTokenType.Colon, "expected ':'");

                switch (name.Text)
                {
                    case "principal":
                        if (principals is not null)
                        {
                            throw _state.Error("duplicate principal declaration in appliesTo");
                        }

                        principals = ParseEntityTypeList();
                        if (principals.Count == 0)
                        {
                            throw _state.Error("principal types must not be empty");
                        }

                        break;
                    case "resource":
                        if (resources is not null)
                        {
                            throw _state.Error("duplicate resource declaration in appliesTo");
                        }

                        resources = ParseEntityTypeList();
                        if (resources.Count == 0)
                        {
                            throw _state.Error("resource types must not be empty");
                        }

                        break;
                    case "context":
                        if (contextRecord is not null || contextPath is not null)
                        {
                            throw _state.Error("duplicate context declaration in appliesTo");
                        }

                        if (_state.Check(SchemaTokenType.LeftBrace))
                        {
                            contextRecord = ParseRecordType();
                        }
                        else
                        {
                            contextPath = new TypeRef(ParsePath());
                        }

                        break;
                    default:
                        throw _state.Error($"expected 'principal', 'resource', or 'context', got {Quote(name.Text)}");
                }

                _state.Match(SchemaTokenType.Comma);
            }

            _state.Expect(SchemaTokenType.RightBrace, "expected '}'");

            if (principals is null)
            {
                throw _state.Error("appliesTo must include a principal declaration");
            }

            if (resources is null)
            {
                throw _state.Error("appliesTo must include a resource declaration");
            }

            return new AppliesToDecl
            {
                Principals = principals,
                Resources = resources,
                ContextRecord = contextRecord,
                ContextPath = contextPath
            };
        }

        private SchemaType ParseType()
        {
            _state.EnterDepth();
            try
            {
                if (_state.Check(SchemaTokenType.LeftBrace))
                {
                    return ParseRecordType();
                }

                if (_state.CheckIdentifier("Set"))
                {
                    _state.Advance();
                    _state.Expect(SchemaTokenType.LeftAngle, "expected '<'");
                    SchemaType element = ParseType();
                    _state.Expect(SchemaTokenType.RightAngle, "expected '>'");
                    return new SetType(element);
                }

                return new TypeRef(ParsePath());
            }
            finally
            {
                _state.ExitDepth();
            }
        }

        private RecordType ParseRecordType()
        {
            _state.Expect(SchemaTokenType.LeftBrace, "expected '{'");

            Dictionary<string, AttributeDecl> attributes = new(StringComparer.Ordinal);
            while (!_state.Check(SchemaTokenType.RightBrace))
            {
                if (_state.IsAtEnd)
                {
                    throw _state.Error("expected '}' to close record type, got EOF");
                }

                IReadOnlyList<SchemaAnnotation> annotations = ParseAnnotations();
                string name = ParseName();
                bool optional = _state.Match(SchemaTokenType.Question);
                _state.Expect(SchemaTokenType.Colon, "expected ':'");

                if (attributes.ContainsKey(name))
                {
                    throw _state.Error($"attribute {Quote(name)} is declared twice");
                }

                attributes.Add(name, new AttributeDecl
                {
                    Type = ParseType(),
                    Optional = optional,
                    Annotations = annotations
                });

                _state.Match(SchemaTokenType.Comma);
            }

            _state.Expect(SchemaTokenType.RightBrace, "expected '}'");
            return new RecordType
            {
                Attributes = attributes
            };
        }

        private static string DescribeToken(SchemaToken token)
        {
            return token.Type switch
            {
                SchemaTokenType.EndOfFile => "EOF",
                SchemaTokenType.Identifier => $"identifier {Quote(token.Text)}",
                SchemaTokenType.String => $"string {Quote(token.Text)}",
                SchemaTokenType.ReservedKeyword => $"reserved keyword {Quote(token.Text)}",
                _ => Quote(token.Text)
            };
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }
    }

    private sealed class ParserState
    {
        private readonly IReadOnlyList<SchemaToken> _tokens;
        private int _index;
        private int _depth;

        public ParserState(IReadOnlyList<SchemaToken> tokens)
        {
            _tokens = tokens;
        }

        public SchemaToken Current => _tokens[_index];

        public SchemaToken Previous => _index == 0 ? _tokens[0] : _tokens[_index - 1];

        public bool IsAtEnd => Current.Type == SchemaTokenType.EndOfFile;

        public bool Check(SchemaTokenType type)
        {
            return Current.Type == type;
        }

        public bool CheckIdentifier(string value)
        {
            return Current.Type == SchemaTokenType.Identifier && string.Equals(Current.Text, value, StringComparison.Ordinal);
        }

        public bool CheckReservedKeyword(string value)
        {
            return Current.Type == SchemaTokenType.ReservedKeyword && string.Equals(Current.Text, value, StringComparison.Ordinal);
        }

        public SchemaToken Advance()
        {
            SchemaToken current = Current;
            if (!IsAtEnd)
            {
                _index++;
            }

            return current;
        }

        public bool Match(SchemaTokenType type)
        {
            if (!Check(type))
            {
                return false;
            }

            Advance();
            return true;
        }

        public SchemaToken Expect(SchemaTokenType type, string message)
        {
            if (!Check(type))
            {
                throw Error(message);
            }

            return Advance();
        }

        public SchemaParseException Error(string message)
        {
            return new SchemaParseException(Current.Position, message);
        }

        public void EnterDepth()
        {
            _depth++;
            if (_depth > MaxDepth)
            {
                throw Error($"maximum parse depth of {MaxDepth} exceeded");
            }
        }

        public void ExitDepth()
        {
            if (_depth > 0)
            {
                _depth--;
            }
        }

        public void SynchronizeDeclaration(bool stopAtRightBrace)
        {
            if (!IsAtEnd)
            {
                Advance();
            }

            while (!IsAtEnd)
            {
                if (Previous.Type == SchemaTokenType.Semicolon)
                {
                    return;
                }

                if (stopAtRightBrace && Current.Type == SchemaTokenType.RightBrace)
                {
                    return;
                }

                if (Current.Type == SchemaTokenType.At)
                {
                    return;
                }

                if (Current.Type == SchemaTokenType.Identifier && (Current.Text == "namespace" || Current.Text == "entity" || Current.Text == "action" || Current.Text == "type"))
                {
                    return;
                }

                Advance();
            }
        }
    }

    private sealed class NamespaceBuilder
    {
        public IReadOnlyList<SchemaAnnotation> Annotations { get; set; } = Array.Empty<SchemaAnnotation>();

        public Dictionary<Ident, EntityDecl> Entities { get; } = [];

        public Dictionary<Ident, EnumDecl> Enums { get; } = [];

        public Dictionary<string, ActionDecl> Actions { get; } = new(StringComparer.Ordinal);

        public Dictionary<Ident, CommonTypeDecl> CommonTypes { get; } = [];

        public NamespaceDecl Build()
        {
            return new NamespaceDecl
            {
                Annotations = Annotations,
                Entities = Entities,
                Enums = Enums,
                Actions = Actions,
                CommonTypes = CommonTypes
            };
        }
    }
}
