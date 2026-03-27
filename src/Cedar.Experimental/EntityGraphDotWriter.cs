using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cedar.Types;

namespace Cedar.Experimental;

public static class EntityGraphDotWriter
{
    public static string ToDot(EntityMap entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        StringWriter writer = new();
        Write(writer, entities);
        return writer.ToString();
    }

    public static void Write(TextWriter writer, EntityMap entities)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(entities);

        writer.WriteLine("strict digraph {");
        writer.WriteLine("\tordering=\"out\"");
        writer.WriteLine("\tnode[shape=box]");

        SortedDictionary<string, List<Entity>> entitiesByType = new(StringComparer.Ordinal);
        foreach (Entity entity in entities)
        {
            string entityType = entity.Uid.Type.Value;
            if (!entitiesByType.TryGetValue(entityType, out List<Entity>? list))
            {
                list = [];
                entitiesByType.Add(entityType, list);
            }

            list.Add(entity);
        }

        foreach ((string entityType, List<Entity> groupedEntities) in entitiesByType)
        {
            writer.WriteLine($"\tsubgraph {Quote("cluster_" + entityType)} {{");
            writer.WriteLine($"\t\tlabel={Quote(entityType)}");
            foreach (Entity entity in groupedEntities)
            {
                writer.WriteLine($"\t\t{Quote(entity.Uid.ToString())} [label={Quote(entity.Uid.Id.Value)}]");
            }

            writer.WriteLine("\t}");
        }

        foreach (Entity entity in entities)
        {
            foreach (EntityUid parent in entity.Parents.OrderBy(static item => item.ToString(), StringComparer.Ordinal))
            {
                writer.WriteLine($"\t{Quote(entity.Uid.ToString())} -> {Quote(parent.ToString())}");
            }
        }

        writer.WriteLine("}");
    }

    private static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        StringBuilder builder = new(value.Length + 2);
        builder.Append('"');
        foreach (char ch in value)
        {
            if (ch == '"' || ch == '\\')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        builder.Append('"');
        return builder.ToString();
    }
}
