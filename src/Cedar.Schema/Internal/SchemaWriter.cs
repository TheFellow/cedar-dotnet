using System;
using System.Collections.Generic;
using System.Text;
using Cedar.Schema;
using Cedar.Types;

namespace Cedar.Schema.Internal;

internal static class SchemaWriter
{
    public static string Write(SchemaDocument schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        StringBuilder builder = new();
        Writer writer = new(builder);
        writer.WriteSchema(schema);
        return builder.ToString();
    }

    private sealed class Writer
    {
        private readonly StringBuilder _builder;
        private int _indent;

        public Writer(StringBuilder builder)
        {
            _builder = builder;
        }

        public void WriteSchema(SchemaDocument schema)
        {
            bool first = true;
            WriteDeclarations(ref first, schema.GlobalNamespace);

            List<string> namespaceNames = new(schema.Namespaces.Keys);
            namespaceNames.Sort(StringComparer.Ordinal);

            for (int index = 0; index < namespaceNames.Count; index++)
            {
                string name = namespaceNames[index];
                NamespaceDecl declaration = schema.Namespaces[name];
                if (!first)
                {
                    _builder.Append('\n');
                }

                first = false;
                WriteAnnotations(declaration.Annotations);
                WriteIndent();
                _builder.Append("namespace ");
                _builder.Append(name);
                _builder.AppendLine(" {");

                _indent++;
                bool innerFirst = true;
                WriteDeclarations(ref innerFirst, declaration);
                _indent--;

                WriteIndent();
                _builder.AppendLine("}");
            }
        }

        private void WriteDeclarations(ref bool first, NamespaceDecl declaration)
        {
            List<Ident> commonTypeNames = SortIdents(declaration.CommonTypes.Keys);
            for (int index = 0; index < commonTypeNames.Count; index++)
            {
                Ident name = commonTypeNames[index];
                CommonTypeDecl commonType = declaration.CommonTypes[name];
                WriteSeparator(ref first);
                WriteAnnotations(commonType.Annotations);
                WriteIndent();
                _builder.Append("type ");
                _builder.Append(name.Value);
                _builder.Append(" = ");
                WriteType(commonType.Type);
                _builder.AppendLine(";");
            }

            List<Ident> entityNames = SortIdents(declaration.Entities.Keys);
            for (int index = 0; index < entityNames.Count; index++)
            {
                Ident name = entityNames[index];
                EntityDecl entity = declaration.Entities[name];
                WriteSeparator(ref first);
                WriteAnnotations(entity.Annotations);
                WriteIndent();
                _builder.Append("entity ");
                _builder.Append(name.Value);

                if (entity.ParentTypes.Count > 0)
                {
                    _builder.Append(" in ");
                    WriteEntityTypes(entity.ParentTypes);
                }

                if (entity.Shape is not null)
                {
                    _builder.Append(' ');
                    WriteRecordType(entity.Shape);
                }

                if (entity.Tags is not null)
                {
                    _builder.Append(" tags ");
                    WriteType(entity.Tags);
                }

                _builder.AppendLine(";");
            }

            List<Ident> enumNames = SortIdents(declaration.Enums.Keys);
            for (int index = 0; index < enumNames.Count; index++)
            {
                Ident name = enumNames[index];
                EnumDecl enumDecl = declaration.Enums[name];
                WriteSeparator(ref first);
                WriteAnnotations(enumDecl.Annotations);
                WriteIndent();
                _builder.Append("entity ");
                _builder.Append(name.Value);
                _builder.Append(" enum [");

                for (int valueIndex = 0; valueIndex < enumDecl.Values.Count; valueIndex++)
                {
                    if (valueIndex > 0)
                    {
                        _builder.Append(", ");
                    }

                    _builder.Append(new CedarString(enumDecl.Values[valueIndex]).MarshalCedar());
                }

                _builder.AppendLine("];");
            }

            List<string> actionNames = new(declaration.Actions.Keys);
            actionNames.Sort(StringComparer.Ordinal);
            for (int index = 0; index < actionNames.Count; index++)
            {
                string name = actionNames[index];
                ActionDecl action = declaration.Actions[name];
                WriteSeparator(ref first);
                WriteAnnotations(action.Annotations);
                WriteIndent();
                _builder.Append("action ");
                WriteName(name);

                if (action.Parents.Count > 0)
                {
                    _builder.Append(" in ");
                    WriteParents(action.Parents);
                }

                if (action.AppliesTo is not null)
                {
                    WriteAppliesTo(action.AppliesTo);
                }

                _builder.AppendLine(";");
            }
        }

        private void WriteAppliesTo(AppliesToDecl appliesTo)
        {
            _builder.AppendLine(" appliesTo {");
            _indent++;

            bool wrote = false;
            if (appliesTo.Principals.Count > 0)
            {
                WriteIndent();
                _builder.Append("principal: ");
                WriteEntityTypes(appliesTo.Principals);
                wrote = true;
            }

            if (appliesTo.Resources.Count > 0)
            {
                if (wrote)
                {
                    _builder.AppendLine(",");
                }

                WriteIndent();
                _builder.Append("resource: ");
                WriteEntityTypes(appliesTo.Resources);
                wrote = true;
            }

            if (appliesTo.Context is not null)
            {
                if (wrote)
                {
                    _builder.AppendLine(",");
                }

                WriteIndent();
                _builder.Append("context: ");
                WriteType(appliesTo.Context);
                wrote = true;
            }

            if (wrote)
            {
                _builder.AppendLine();
            }

            _indent--;
            WriteIndent();
            _builder.Append('}');
        }

        private void WriteEntityTypes(IReadOnlyList<EntityType> entityTypes)
        {
            if (entityTypes.Count == 1)
            {
                _builder.Append(entityTypes[0].Value);
                return;
            }

            _builder.Append('[');
            for (int index = 0; index < entityTypes.Count; index++)
            {
                if (index > 0)
                {
                    _builder.Append(", ");
                }

                _builder.Append(entityTypes[index].Value);
            }

            _builder.Append(']');
        }

        private void WriteParents(IReadOnlyList<ParentRef> parents)
        {
            if (parents.Count == 1)
            {
                WriteParent(parents[0]);
                return;
            }

            _builder.Append('[');
            for (int index = 0; index < parents.Count; index++)
            {
                if (index > 0)
                {
                    _builder.Append(", ");
                }

                WriteParent(parents[index]);
            }

            _builder.Append(']');
        }

        private void WriteParent(ParentRef parent)
        {
            if (parent.Type is null)
            {
                WriteName(parent.Id);
                return;
            }

            _builder.Append(parent.Type.Value);
            _builder.Append("::");
            _builder.Append(new CedarString(parent.Id).MarshalCedar());
        }

        private void WriteType(SchemaType type)
        {
            switch (type)
            {
                case StringType:
                    _builder.Append("String");
                    return;
                case LongType:
                    _builder.Append("Long");
                    return;
                case BoolType:
                    _builder.Append("Bool");
                    return;
                case ExtensionType extension:
                    _builder.Append(extension.Name.Value);
                    return;
                case SetType setType:
                    _builder.Append("Set<");
                    WriteType(setType.Element);
                    _builder.Append('>');
                    return;
                case RecordType recordType:
                    WriteRecordType(recordType);
                    return;
                case EntityTypeRef entityType:
                    _builder.Append(entityType.Name.Value);
                    return;
                case TypeRef typeRef:
                    _builder.Append(typeRef.Name);
                    return;
                default:
                    throw new InvalidOperationException($"unsupported schema type: {type.GetType().FullName}");
            }
        }

        private void WriteRecordType(RecordType recordType)
        {
            _builder.Append('{');

            List<string> names = new(recordType.Attributes.Keys);
            names.Sort(StringComparer.Ordinal);
            if (names.Count > 0)
            {
                _builder.AppendLine();
                _indent++;

                for (int index = 0; index < names.Count; index++)
                {
                    string name = names[index];
                    AttributeDecl attribute = recordType.Attributes[name];
                    WriteAnnotations(attribute.Annotations);
                    WriteIndent();
                    WriteName(name);
                    if (attribute.Optional)
                    {
                        _builder.Append('?');
                    }

                    _builder.Append(": ");
                    WriteType(attribute.Type);
                    if (index < names.Count - 1)
                    {
                        _builder.Append(',');
                    }

                    _builder.AppendLine();
                }

                _indent--;
                WriteIndent();
            }

            _builder.Append('}');
        }

        private void WriteName(string value)
        {
            if (CanWriteIdentifier(value))
            {
                _builder.Append(value);
            }
            else
            {
                _builder.Append(new CedarString(value).MarshalCedar());
            }
        }

        private void WriteAnnotations(IReadOnlyList<SchemaAnnotation> annotations)
        {
            List<SchemaAnnotation> sorted = new(annotations);
            sorted.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Key.Value, right.Key.Value));

            for (int index = 0; index < sorted.Count; index++)
            {
                SchemaAnnotation annotation = sorted[index];
                WriteIndent();
                _builder.Append('@');
                _builder.Append(annotation.Key.Value);
                if (annotation.Value.Length > 0)
                {
                    _builder.Append('(');
                    _builder.Append(new CedarString(annotation.Value).MarshalCedar());
                    _builder.Append(')');
                }

                _builder.AppendLine();
            }
        }

        private void WriteIndent()
        {
            for (int index = 0; index < _indent; index++)
            {
                _builder.Append('\t');
            }
        }

        private static bool CanWriteIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || SchemaTokenizer.IsReservedKeyword(value))
            {
                return false;
            }

            if (!IsIdentifierStart(value[0]))
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                if (!IsIdentifierPart(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsIdentifierStart(char value)
        {
            return value == '_' || value is >= 'A' and <= 'Z' || value is >= 'a' and <= 'z';
        }

        private static bool IsIdentifierPart(char value)
        {
            return IsIdentifierStart(value) || value is >= '0' and <= '9';
        }

        private static List<Ident> SortIdents(IEnumerable<Ident> values)
        {
            List<Ident> result = new(values);
            result.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
            return result;
        }

        private void WriteSeparator(ref bool first)
        {
            if (!first)
            {
                _builder.Append('\n');
            }

            first = false;
        }
    }
}
