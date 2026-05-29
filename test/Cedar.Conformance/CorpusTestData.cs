using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Parser;
using Cedar.Schema;
using Cedar.Types;

namespace Cedar.Conformance;

public sealed record CorpusScenarioRequest(
    int RequestIndex,
    Request Request,
    Decision ExpectedDecision,
    ImmutableArray<string> ExpectedReasons,
    ImmutableArray<string> ExpectedErrors)
{
    public override string ToString()
    {
        return $"#{RequestIndex}";
    }
}

public sealed record CorpusScenarioCase(
    string ScenarioFile,
    PolicySet Policies,
    EntityMap Entities,
    ImmutableArray<CorpusScenarioRequest> Requests,
    string? SchemaPath,
    string? SchemaText,
    string? RustSchemaJsonPath,
    string? RustSchemaJson,
    string? ValidationPath,
    CorpusValidationDocument? Validation)
{
    public override string ToString()
    {
        return ScenarioFile;
    }
}

public sealed class CorpusValidationDocument
{
    [JsonPropertyName("policyValidation")]
    public CorpusPolicyValidationResult PolicyValidation { get; init; } = new();

    [JsonPropertyName("entityValidation")]
    public CorpusEntityValidationResult EntityValidation { get; init; } = new();

    [JsonPropertyName("requestValidation")]
    public List<CorpusRequestValidationResult> RequestValidation { get; init; } = [];
}

public sealed class CorpusPolicyValidationResult
{
    [JsonPropertyName("strict")]
    public bool Strict { get; init; }

    [JsonPropertyName("permissive")]
    public bool Permissive { get; init; }

    [JsonPropertyName("strictErrors")]
    public List<string> StrictErrors { get; init; } = [];

    [JsonPropertyName("permissiveErrors")]
    public List<string> PermissiveErrors { get; init; } = [];

    [JsonPropertyName("perPolicy")]
    public Dictionary<string, CorpusPolicyValidationResult> PerPolicy { get; init; }
        = new(StringComparer.Ordinal);
}

public sealed class CorpusEntityValidationResult
{
    [JsonPropertyName("perEntity")]
    public Dictionary<string, CorpusValidationEntityResult> PerEntity { get; init; }
        = new(StringComparer.Ordinal);
}

public sealed class CorpusValidationEntityResult
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalData { get; init; }
        = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}

public sealed class CorpusRequestValidationResult
{
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("strict")]
    public bool Strict { get; init; }

    [JsonPropertyName("permissive")]
    public bool Permissive { get; init; }
}

public static class CorpusTestData
{
    private const long MaxArchiveBytes = 512L * 1024L * 1024L;
    private const long MaxEntryBytes = 16L * 1024L * 1024L;
    private const string CorpusArchiveName = "corpus-tests.tar.gz";
    private const string CorpusArchiveRoot = "corpus-tests/";
    private const string JsonSchemaArchiveName = "corpus-tests-json-schemas.tar.gz";
    private const string JsonSchemaArchiveRoot = "corpus-tests-json-schemas/";
    private const string ValidationArchiveName = "corpus-tests-validation.tar.gz";
    private const string ValidationArchiveRoot = "corpus-tests-validation/";

    private static readonly Lazy<IReadOnlyList<CorpusScenarioCase>> CachedScenarios = new(LoadScenarios);
    private static readonly Lazy<Dictionary<string, CorpusScenarioCase>> ScenarioIndex = new(BuildScenarioIndex);
    private static readonly JsonSerializerOptions ValidationJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<CorpusScenarioCase> GetAllScenarios()
    {
        return CachedScenarios.Value;
    }

    public static CorpusScenarioCase GetScenario(string scenarioFile)
    {
        return ScenarioIndex.Value[scenarioFile];
    }

    public static IReadOnlyList<CorpusScenarioCase> GetSchemaScenarios()
    {
        return CachedScenarios.Value.Where(static scenario => scenario.SchemaText is not null).ToList();
    }

    public static IEnumerable<object[]> RequestKeys
    {
        get
        {
            foreach (CorpusScenarioCase scenario in CachedScenarios.Value)
            {
                for (int i = 0; i < scenario.Requests.Length; i++)
                {
                    yield return [scenario.ScenarioFile, i];
                }
            }
        }
    }

    public static IEnumerable<object[]> ScenarioKeys
    {
        get
        {
            foreach (CorpusScenarioCase scenario in CachedScenarios.Value)
            {
                yield return [scenario.ScenarioFile];
            }
        }
    }

    public static IEnumerable<object[]> SchemaKeys
    {
        get
        {
            foreach (CorpusScenarioCase scenario in CachedScenarios.Value)
            {
                if (scenario.SchemaText is not null)
                {
                    yield return [scenario.ScenarioFile];
                }
            }
        }
    }

    public static IEnumerable<object[]> ValidationKeys
    {
        get
        {
            foreach (CorpusScenarioCase scenario in CachedScenarios.Value)
            {
                if (scenario.Validation is not null)
                {
                    yield return [scenario.ScenarioFile];
                }
            }
        }
    }

    private static Dictionary<string, CorpusScenarioCase> BuildScenarioIndex()
    {
        Dictionary<string, CorpusScenarioCase> index = new(StringComparer.Ordinal);
        foreach (CorpusScenarioCase scenario in CachedScenarios.Value)
        {
            index[scenario.ScenarioFile] = scenario;
        }

        return index;
    }

    public static string LocateTestDataArchive(string archiveName)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveName);

        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(directory, "testdata", archiveName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            DirectoryInfo? parent = Directory.GetParent(directory);
            directory = parent?.FullName;
        }

        throw new FileNotFoundException($"Unable to locate testdata/{archiveName} from AppContext.BaseDirectory.");
    }

    public static Dictionary<string, byte[]> ExtractArchive(string archivePath, string requiredRootPrefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(archivePath);
        ArgumentException.ThrowIfNullOrEmpty(requiredRootPrefix);

        using FileStream fileStream = File.OpenRead(archivePath);
        using GZipStream gzip = new(fileStream, CompressionMode.Decompress, leaveOpen: false);
        using TarReader tarReader = new(gzip, leaveOpen: false);

        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        long extractedBytes = 0;

        while (tarReader.GetNextEntry() is TarEntry entry)
        {
            if (entry.EntryType is not TarEntryType.RegularFile and not TarEntryType.V7RegularFile)
            {
                continue;
            }

            if (entry.DataStream is null)
            {
                continue;
            }

            string normalizedPath = NormalizePath(entry.Name);
            if (normalizedPath.Length == 0
                || !normalizedPath.StartsWith(requiredRootPrefix, StringComparison.Ordinal)
                || IsAppleDoubleEntry(normalizedPath))
            {
                continue;
            }

            byte[] data = ReadAllBytesBounded(entry.DataStream, MaxEntryBytes);
            extractedBytes += data.LongLength;
            if (extractedBytes > MaxArchiveBytes)
            {
                throw new InvalidDataException($"Corpus archive exceeds {MaxArchiveBytes} bytes after extraction.");
            }

            files[normalizedPath] = data;
        }

        return files;
    }

    private static IReadOnlyList<CorpusScenarioCase> LoadScenarios()
    {
        Dictionary<string, byte[]> files = ExtractArchive(LocateTestDataArchive(CorpusArchiveName), CorpusArchiveRoot);
        Dictionary<string, byte[]> rustSchemaFiles = ExtractArchive(LocateTestDataArchive(JsonSchemaArchiveName), JsonSchemaArchiveRoot);
        Dictionary<string, byte[]> validationFiles = ExtractArchive(LocateTestDataArchive(ValidationArchiveName), ValidationArchiveRoot);

        List<string> scenarioFiles = files.Keys
            .Where(static file => file.StartsWith(CorpusArchiveRoot, StringComparison.Ordinal))
            .Where(static file => file.EndsWith(".json", StringComparison.Ordinal))
            .Where(static file => !file.EndsWith(".entities.json", StringComparison.Ordinal))
            .OrderBy(static file => file, StringComparer.Ordinal)
            .ToList();

        List<CorpusScenarioCase> scenarios = [];
        foreach (string scenarioFile in scenarioFiles)
        {
            using JsonDocument scenarioDocument = JsonDocument.Parse(ReadRequiredFile(files, scenarioFile));
            JsonElement scenarioRoot = scenarioDocument.RootElement;
            if (scenarioRoot.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Scenario '{scenarioFile}' must be a JSON object.");
            }

            string policiesPath = GetRequiredString(scenarioRoot, "policies");
            string entitiesPath = GetRequiredString(scenarioRoot, "entities");
            string policyText = ReadRequiredTextFile(files, policiesPath);
            byte[] entitiesBytes = ReadRequiredFile(files, entitiesPath);

            string? schemaPath = null;
            string? schemaText = null;
            string? rustSchemaJsonPath = null;
            string? rustSchemaJson = null;
            CorpusValidationDocument? validation = null;
            string? validationPath = null;

            if (scenarioRoot.TryGetProperty("schema", out JsonElement schemaElement))
            {
                schemaPath = GetSchemaPath(schemaElement);
                schemaText = ReadRequiredTextFile(files, schemaPath);
                rustSchemaJsonPath = GetRustSchemaJsonPath(schemaPath);
                rustSchemaJson = ReadRequiredTextFile(rustSchemaFiles, rustSchemaJsonPath);
                validationPath = GetValidationPath(scenarioFile);
                validation = ParseValidation(ReadRequiredTextFile(validationFiles, validationPath));
            }

            PolicySet policySet = BuildPolicySet(policyText);
            EntityMap entityMap;
            if (schemaText is not null && schemaPath is not null)
            {
                try
                {
                    entityMap = ParseEntityMapWithSchema(entitiesBytes, schemaText, schemaPath);
                }
                catch
                {
                    // Fall back to non-schema parsing if schema-guided parsing fails
                    // (e.g. unrecognized extension types like __cedar::datetime).
                    entityMap = ParseEntityMap(entitiesBytes);
                }
            }
            else
            {
                entityMap = ParseEntityMap(entitiesBytes);
            }

            JsonElement requestsElement = GetRequiredProperty(scenarioRoot, "requests", JsonValueKind.Array);
            ImmutableArray<CorpusScenarioRequest>.Builder requestBuilder = ImmutableArray.CreateBuilder<CorpusScenarioRequest>();
            int requestIndex = 0;
            foreach (JsonElement requestElement in requestsElement.EnumerateArray())
            {
                Request request = ParseRequest(requestElement);
                Decision decision = ParseDecision(GetRequiredString(requestElement, "decision"));
                ImmutableArray<string> reasons = ReadStringArray(requestElement, "reason");
                ImmutableArray<string> errors = ReadStringArray(requestElement, "errors");

                requestBuilder.Add(new CorpusScenarioRequest(
                    requestIndex,
                    request,
                    decision,
                    reasons,
                    errors));
                requestIndex++;
            }

            scenarios.Add(new CorpusScenarioCase(
                scenarioFile,
                policySet,
                entityMap,
                requestBuilder.ToImmutable(),
                schemaPath,
                schemaText,
                rustSchemaJsonPath,
                rustSchemaJson,
                validationPath,
                validation));
        }

        return scenarios;
    }

    private static byte[] ReadAllBytesBounded(Stream stream, long maxBytes)
    {
        using MemoryStream output = new();
        byte[] buffer = new byte[16 * 1024];
        long total = 0;

        while (true)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException($"Single corpus entry exceeds {maxBytes} bytes.");
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static bool IsAppleDoubleEntry(string normalizedPath)
    {
        string fileName = Path.GetFileName(normalizedPath);
        return fileName.StartsWith("._", StringComparison.Ordinal);
    }

    private static string GetRustSchemaJsonPath(string schemaPath)
    {
        return $"{JsonSchemaArchiveRoot}{Path.GetFileName(schemaPath)}.json";
    }

    private static string GetValidationPath(string scenarioFile)
    {
        return $"{ValidationArchiveRoot}{Path.GetFileNameWithoutExtension(scenarioFile)}.validation.json";
    }

    private static CorpusValidationDocument ParseValidation(string json)
    {
        return JsonSerializer.Deserialize<CorpusValidationDocument>(json, ValidationJsonOptions)
            ?? throw new InvalidDataException("Validation JSON deserialized to null.");
    }

    private static PolicySet BuildPolicySet(string policyText)
    {
        PolicyAst[] ast = CedarParser.ParsePolicies(policyText);
        Policy[] policies = Policy.UnmarshalCedarList(policyText);
        if (policies.Length != ast.Length)
        {
            throw new InvalidDataException("Parser and policy unmarshaler returned different policy counts.");
        }

        PolicySet set = new();
        for (int index = 0; index < policies.Length; index++)
        {
            set.Add(new PolicyId($"policy{index}"), policies[index]);
        }

        return set;
    }

    private static EntityMap ParseEntityMap(byte[] json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Entity file must contain a JSON array.");
        }

        List<Entity> entities = [];
        foreach (JsonElement entityElement in document.RootElement.EnumerateArray())
        {
            entities.Add(ParseEntity(entityElement));
        }

        return new EntityMap(entities);
    }

    private static EntityMap ParseEntityMapWithSchema(byte[] entityJson, string schemaText, string schemaPath)
    {
        ArgumentNullException.ThrowIfNull(entityJson);
        ArgumentNullException.ThrowIfNull(schemaText);
        ArgumentNullException.ThrowIfNull(schemaPath);

        SchemaDocument schema = SchemaDocument.UnmarshalCedar(schemaText, schemaPath);
        return SchemaGuidedEntityParser.ParseEntityMap(entityJson, schema);
    }

    private static Entity ParseEntity(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Entity values must be JSON objects.");
        }

        EntityUid uid = ParseEntityUid(GetRequiredProperty(element, "uid", JsonValueKind.Object));
        EntityUidSet parents = element.TryGetProperty("parents", out JsonElement parentsElement)
            ? ParseParents(parentsElement)
            : new EntityUidSet();
        CedarRecord attributes = element.TryGetProperty("attrs", out JsonElement attrsElement)
            ? ParseRecord(attrsElement, "attrs")
            : new CedarRecord();
        CedarRecord tags = element.TryGetProperty("tags", out JsonElement tagsElement)
            ? ParseRecord(tagsElement, "tags")
            : new CedarRecord();

        return new Entity(uid, parents, attributes, tags);
    }

    private static EntityUidSet ParseParents(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Entity parents must be an array.");
        }

        List<EntityUid> values = [];
        foreach (JsonElement parent in element.EnumerateArray())
        {
            values.Add(ParseEntityUid(parent));
        }

        return new EntityUidSet(values);
    }

    private static Request ParseRequest(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Request values must be JSON objects.");
        }

        EntityUid principal = ParseEntityUid(GetRequiredProperty(element, "principal", JsonValueKind.Object));
        EntityUid action = ParseEntityUid(GetRequiredProperty(element, "action", JsonValueKind.Object));
        EntityUid resource = ParseEntityUid(GetRequiredProperty(element, "resource", JsonValueKind.Object));
        CedarRecord context = element.TryGetProperty("context", out JsonElement contextElement)
            ? ParseRecord(contextElement, "context")
            : new CedarRecord();

        return new Request(principal, action, resource, context);
    }

    private static Decision ParseDecision(string value)
    {
        return value switch
        {
            "allow" => Decision.Allow,
            "deny" => Decision.Deny,
            _ => throw new InvalidDataException($"Unsupported decision '{value}'.")
        };
    }

    private static ImmutableArray<string> ReadStringArray(JsonElement objectElement, string propertyName)
    {
        if (!objectElement.TryGetProperty(propertyName, out JsonElement value))
        {
            return ImmutableArray<string>.Empty;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Property '{propertyName}' must be an array.");
        }

        ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"Property '{propertyName}' must only contain strings.");
            }

            builder.Add(item.GetString() ?? string.Empty);
        }

        return builder.ToImmutable();
    }

    private static CedarRecord ParseRecord(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return new CedarRecord();
        }

        ICedarData value = ParseCedarValue(element);
        return value as CedarRecord ?? throw new InvalidDataException($"Property '{name}' must be an object.");
    }

    private static ICedarData ParseCedarValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => CedarBool.True,
            JsonValueKind.False => CedarBool.False,
            JsonValueKind.Number => ParseLong(element),
            JsonValueKind.String => new CedarString(element.GetString() ?? string.Empty),
            JsonValueKind.Array => ParseSet(element),
            JsonValueKind.Object => ParseObject(element),
            _ => throw new InvalidDataException($"Unsupported JSON token '{element.ValueKind}' in Cedar value.")
        };
    }

    private static CedarLong ParseLong(JsonElement element)
    {
        if (!element.TryGetInt64(out long value))
        {
            throw new InvalidDataException("Numeric values must fit in signed 64-bit range.");
        }

        return new CedarLong(value);
    }

    private static CedarSet ParseSet(JsonElement element)
    {
        List<ICedarData> values = [];
        foreach (JsonElement child in element.EnumerateArray())
        {
            values.Add(ParseCedarValue(child));
        }

        return new CedarSet(values);
    }

    private static ICedarData ParseObject(JsonElement element)
    {
        if (TryParseEntityUid(element, out EntityUid? uid))
        {
            return uid!;
        }

        if (TryParseExtension(element, out ICedarData? extensionValue))
        {
            return extensionValue!;
        }

        RecordMap values = [];
        foreach (JsonProperty property in element.EnumerateObject())
        {
            values.Add(new CedarString(property.Name), ParseCedarValue(property.Value));
        }

        return new CedarRecord(values);
    }

    private static EntityUid ParseEntityUid(JsonElement element)
    {
        if (!TryParseEntityUid(element, out EntityUid? uid))
        {
            throw new InvalidDataException("Expected entity uid object in {type,id} or {__entity:{type,id}} format.");
        }

        return uid!;
    }

    private static bool TryParseEntityUid(JsonElement element, out EntityUid? uid)
    {
        uid = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonElement payload = element;
        if (element.TryGetProperty("__entity", out JsonElement explicitEntity))
        {
            if (explicitEntity.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            payload = explicitEntity;
        }

        if (!payload.TryGetProperty("type", out JsonElement typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (!payload.TryGetProperty("id", out JsonElement idElement) || idElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string type = typeElement.GetString() ?? string.Empty;
        string id = idElement.GetString() ?? string.Empty;
        uid = new EntityUid(new EntityType(type), new CedarString(id));
        return true;
    }

    private static bool TryParseExtension(JsonElement element, out ICedarData? value)
    {
        value = null;
        if (!TryGetExtensionPayload(element, out JsonElement payload))
        {
            return false;
        }

        string fn = GetRequiredString(payload, "fn");
        string arg = GetRequiredString(payload, "arg");

        try
        {
            value = fn switch
            {
                "decimal" => CedarDecimal.Parse(arg),
                "datetime" => CedarDatetime.Parse(arg),
                "duration" => CedarDuration.Parse(arg),
                "ip" => CedarIpAddress.Parse(arg),
                "pattern" => CedarPattern.Parse(arg),
                _ => null
            };
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or FormatException or OverflowException)
        {
            value = null;
            return false;
        }

        return value is not null;
    }

    private static bool TryGetExtensionPayload(JsonElement element, out JsonElement payload)
    {
        if (element.TryGetProperty("__extn", out JsonElement explicitExtension))
        {
            if (explicitExtension.ValueKind == JsonValueKind.Object
                && explicitExtension.TryGetProperty("fn", out JsonElement explicitFn)
                && explicitFn.ValueKind == JsonValueKind.String
                && explicitExtension.TryGetProperty("arg", out JsonElement explicitArg)
                && explicitArg.ValueKind == JsonValueKind.String)
            {
                payload = explicitExtension;
                return true;
            }

            payload = default;
            return false;
        }

        if (element.TryGetProperty("fn", out JsonElement functionElement)
            && functionElement.ValueKind == JsonValueKind.String
            && element.TryGetProperty("arg", out JsonElement argumentElement)
            && argumentElement.ValueKind == JsonValueKind.String)
        {
            payload = element;
            return true;
        }

        payload = default;
        return false;
    }

    private static JsonElement GetRequiredProperty(JsonElement objectElement, string propertyName, JsonValueKind expectedKind)
    {
        if (!objectElement.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new InvalidDataException($"Missing required property '{propertyName}'.");
        }

        if (value.ValueKind != expectedKind)
        {
            throw new InvalidDataException($"Property '{propertyName}' must be '{expectedKind}', got '{value.ValueKind}'.");
        }

        return value;
    }

    private static string GetRequiredString(JsonElement objectElement, string propertyName)
    {
        JsonElement value = GetRequiredProperty(objectElement, propertyName, JsonValueKind.String);
        return value.GetString() ?? string.Empty;
    }

    private static string GetSchemaPath(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Property 'schema' must be 'String', got '{element.ValueKind}'.");
        }

        return element.GetString() ?? string.Empty;
    }

    private static byte[] ReadRequiredFile(IReadOnlyDictionary<string, byte[]> files, string path)
    {
        string normalizedPath = NormalizePath(path);
        if (!files.TryGetValue(normalizedPath, out byte[]? data))
        {
            throw new FileNotFoundException($"Missing referenced corpus file '{normalizedPath}'.");
        }

        return data;
    }

    private static string ReadRequiredTextFile(IReadOnlyDictionary<string, byte[]> files, string path)
    {
        return Encoding.UTF8.GetString(ReadRequiredFile(files, path));
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException($"Absolute paths are not allowed in archive entries: '{path}'.");
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new InvalidDataException($"Unsafe path traversal in archive entry: '{path}'.");
            }
        }

        return string.Join('/', segments);
    }
}
